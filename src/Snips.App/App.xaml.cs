using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using Snips.Core.Domain;
using Snips.Core.Storage;
using Snips.Data;
using Snips.Interop.Foreground;
using Snips.Interop.Hotkeys;
using Wpf.Ui.Appearance;

namespace Snips.App;

public partial class App : Application
{
    private const uint VK_SPACE = 0x20;
    private const uint VK_S = 0x53;
    private const uint VK_BACKTICK = 0xC0; // VK_OEM_3

    // Tried in order at startup until one registers. Ctrl+Alt+Space is claimed by enough
    // other clipboard/snippet/IME tools that it needs backups rather than a single swap —
    // a real per-user rebind UI is Phase 6 (SPEC.md §5.8); this is the stopgap until then.
    private static readonly (HotKeyModifiers Modifiers, uint VirtualKey, string Label)[] HotkeyCandidates =
    [
        (HotKeyModifiers.Control | HotKeyModifiers.Alt, VK_SPACE, "Ctrl+Alt+Space"),
        (HotKeyModifiers.Control | HotKeyModifiers.Alt, VK_S, "Ctrl+Alt+S"),
        (HotKeyModifiers.Control | HotKeyModifiers.Shift, VK_SPACE, "Ctrl+Shift+Space"),
        (HotKeyModifiers.Control | HotKeyModifiers.Alt, VK_BACKTICK, "Ctrl+Alt+`"),
    ];

    private const string SingleInstanceMutexName = "Local\\Snips.SingleInstance";
    private const string ShowRequestEventName = "Local\\Snips.ShowRequest";

    private SnipsDatabase? _database;
    private ForegroundWindowTracker? _foregroundTracker;
    private HotKeyManager? _hotKeyManager;
    private MainWindow? _mainWindow;
    private Mutex? _singleInstanceMutex;
    private EventWaitHandle? _showRequestEvent;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // No global handler existed anywhere before this (flagged in a full-codebase review,
        // 2026-07-24). The specific crash it caught — a malformed date/filter format string
        // reaching an unguarded .ToString(format) — is now fixed at the source in
        // BuiltInVariables/TemplateFilters, but this stays as the actual safety net: almost
        // every UI event handler in this app is async void (SelectionChanged, drag/drop,
        // favorite toggling, ...), and an unhandled exception from ANY of them terminates the
        // whole tray app instantly with zero feedback, for a mistake as small as browsing to the
        // wrong row. Marking e.Handled = true is what keeps the app alive instead of crashing;
        // the message box is just so it's not a silent swallow.
        DispatcherUnhandledException += (_, ex) =>
        {
            MessageBox.Show(
                $"Snips hit an unexpected error and stayed open, but this snippet or action may not have worked:\n\n{ex.Exception.Message}",
                "Snips", MessageBoxButton.OK, MessageBoxImage.Warning);
            ex.Handled = true;
        };

        // Snips always starts hidden in the tray by design (SPEC.md §10) — which means a
        // second launch (e.g. double-clicking the desktop shortcut again while it's already
        // running) previously just started an invisible duplicate process with no feedback
        // at all. That's exactly what Roland hit and reported as "does nothing." Enforce a
        // single instance and have a relaunch just show the existing window instead.
        _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);
        _showRequestEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowRequestEventName);

        if (!createdNew)
        {
            _showRequestEvent.Set();
            Shutdown();
            return;
        }

        // Fluent 2's accent is normally the Windows system accent; Snips keeps its own
        // warm-yellow identity from SPEC.md §5.2 instead (systemAccentColor: false pins it
        // regardless of what the user's Windows accent color is set to).
        ApplicationAccentColorManager.Apply(
            Color.FromRgb(0xE0, 0xA8, 0x00), ApplicationTheme.Light,
            systemGlassColor: false, systemAccentColor: false);

        var dbPath = ResolveDatabasePath();
        _database = await SnipsDatabase.OpenAsync(dbPath);
        await SeedExampleSnippetsIfEmptyAsync(_database);

        // Must exist before the window so WM_HOTKEY delivery (via SetWinEventHook,
        // WINEVENT_OUTOFCONTEXT) starts as soon as the Dispatcher message loop is pumping.
        _foregroundTracker = new ForegroundWindowTracker();

        var externalVariablesPath = DatabasePathResolver.ResolveExternalVariablesPath(dbPath);
        _mainWindow = new MainWindow(_database, _foregroundTracker, externalVariablesPath);
        _hotKeyManager = new HotKeyManager(_mainWindow);

        var boundLabel = RegisterFirstAvailableHotkey();

        _mainWindow.TrayIcon.ToolTipText = boundLabel is null
            ? $"Snips, {BuildIdentifier.Value} (no hotkey bound — open from this tray icon)"
            : $"Snips, {BuildIdentifier.Value} — {boundLabel} to open";

        if (boundLabel is null)
        {
            MessageBox.Show(
                "All of Snips' candidate hotkeys (Ctrl+Alt+Space, Ctrl+Alt+S, Ctrl+Shift+Space, " +
                "Ctrl+Alt+`) are already claimed by other applications. Open Snips from the tray " +
                "icon instead until a rebindable Settings view exists.",
                "Snips", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else if (boundLabel != HotkeyCandidates[0].Label && await _database.Settings.GetAsync("HotkeyFallbackNoticeShown") is null)
        {
            // Shown once ever, not on every launch — this was genuinely nagging Roland during
            // repeated dev-cycle relaunches and reads as "the app is broken" rather than as the
            // one-time heads-up it's meant to be.
            MessageBox.Show(
                $"Ctrl+Alt+Space is already in use by another application, so Snips bound " +
                $"{boundLabel} instead. Press {boundLabel} to open the picker. " +
                "(This notice won't be shown again.)",
                "Snips", MessageBoxButton.OK, MessageBoxImage.Information);
            await _database.Settings.SetAsync("HotkeyFallbackNoticeShown", "1");
        }

        await RegisterPerSnippetHotkeysAsync();

        _ = ListenForShowRequestsAsync();

        // Intentionally not shown here — starts minimised to the tray (SPEC.md §10 default).
    }

    /// <summary>Runs for the lifetime of the app, waiting on a background thread for a later
    /// launch to signal "show yourself" (see the single-instance check in OnStartup).</summary>
    private async Task ListenForShowRequestsAsync()
    {
        while (true)
        {
            await Task.Run(() => _showRequestEvent!.WaitOne());
            _mainWindow?.ShowAndFocus();
        }
    }

    /// <summary>
    /// SPEC.md §5.8: each snippet may additionally have its own global hotkey that applies it
    /// directly, without opening the picker. Failures here (combo claimed by something else)
    /// are reported once as a batch rather than crashing startup — the app remains fully usable
    /// via the picker either way. A per-shortcut "⚠ inactive, retry on focus" indicator in the
    /// list (as SPEC.md §5.8 describes) is not built yet; this only surfaces failures at startup.
    /// </summary>
    private async Task RegisterPerSnippetHotkeysAsync()
    {
        var shortcuts = await _database!.Shortcuts.ListAllAsync();
        var failedSnippetNames = new List<string>();

        foreach (var shortcut in shortcuts)
        {
            var snippetId = shortcut.SnippetId;
            var id = _hotKeyManager!.Register(
                (HotKeyModifiers)shortcut.Modifiers, (uint)shortcut.VirtualKey,
                () => _mainWindow!.ApplySnippetByHotkey(snippetId));

            if (id is null)
            {
                var snippet = await _database.Snippets.GetByIdAsync(snippetId);
                failedSnippetNames.Add(snippet?.Name ?? snippetId);
            }
        }

        if (failedSnippetNames.Count > 0)
        {
            MessageBox.Show(
                "These snippets have a shortcut assigned, but the combination is already in " +
                $"use by another application: {string.Join(", ", failedSnippetNames)}. Define a " +
                "different shortcut for them from the picker's right-click menu.",
                "Snips", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private string? RegisterFirstAvailableHotkey()
    {
        foreach (var candidate in HotkeyCandidates)
        {
            var id = _hotKeyManager!.Register(candidate.Modifiers, candidate.VirtualKey, _mainWindow!.ShowAndFocus);
            if (id is not null)
                return candidate.Label;
        }

        return null;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotKeyManager?.Dispose();
        _foregroundTracker?.Dispose();
        _database?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _showRequestEvent?.Dispose();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }

    private static string ResolveDatabasePath()
    {
        var exeDirectory = AppContext.BaseDirectory;
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var portableMarkerExists = File.Exists(Path.Combine(exeDirectory, DatabasePathResolver.PortableMarkerFileName));
        return DatabasePathResolver.Resolve(exeDirectory, localAppData, portableMarkerExists);
    }

    /// <summary>
    /// First-run only (empty library): a handful of examples that actually exercise the
    /// variable engine, so opening the picker for the first time shows something working
    /// rather than an empty list. SPEC.md §15 Q5 calls for 8–10 eventually; this is a subset.
    /// </summary>
    private static async Task SeedExampleSnippetsIfEmptyAsync(SnipsDatabase database)
    {
        if ((await database.Snippets.ListAsync()).Count > 0)
            return;

        var examples = new (string Name, string Description, string Body)[]
        {
            ("Meeting follow-up", "Sent after a client call",
                "Dear {{input:Name}},\n\nThank you for your time today, {{date}}. I'll follow up " +
                "with next steps by {{date:+3d:dd.MM.yyyy}}.\n\nBest regards,\n{{user}}"),
            ("Email signature", "Quick plain-text signature",
                "{{user}}\n{{useremail}}\n{{date}}"),
            ("Bug report template", "Starting point for a bug report",
                "Environment: {{os}} ({{osversion}})\nReported by: {{user}} on {{date}}\n\n" +
                "Steps to reproduce:\n1. \n2. \n\nExpected:\nActual:"),
            ("Quick timestamp", "Paste the current date and time", "{{datetime}}"),
        };

        foreach (var (name, description, body) in examples)
        {
            var now = DateTime.UtcNow;
            await database.Snippets.CreateAsync(new Snippet
            {
                Id = "unused",
                Name = name,
                Description = description,
                BodyHtml = $"<p>{body.Replace("\n", "<br/>")}</p>",
                PlainText = body,
                IsRichText = false,
                CreatedUtc = now,
                ModifiedUtc = now,
            });
        }
    }
}
