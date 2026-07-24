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
    // No stored Label here (there used to be one, hardcoded in English) — HotKeyModifiers'
    // numeric values are identical to HotkeyValidator's Mod* constants (both mirror the raw
    // Win32 MOD_* bits), so HotkeyFormatting.Format(candidate) produces the same label on
    // demand, correctly localized, instead of two independent copies that could drift.
    private static readonly (HotKeyModifiers Modifiers, uint VirtualKey)[] HotkeyCandidates =
    [
        (HotKeyModifiers.Control | HotKeyModifiers.Alt, VK_SPACE),
        (HotKeyModifiers.Control | HotKeyModifiers.Alt, VK_S),
        (HotKeyModifiers.Control | HotKeyModifiers.Shift, VK_SPACE),
        (HotKeyModifiers.Control | HotKeyModifiers.Alt, VK_BACKTICK),
    ];

    private static string FormatCandidateLabel((HotKeyModifiers Modifiers, uint VirtualKey) candidate) =>
        HotkeyFormatting.Format((int)candidate.Modifiers, (int)candidate.VirtualKey);

    private const string SingleInstanceMutexName = "Local\\Snips.SingleInstance";
    private const string ShowRequestEventName = "Local\\Snips.ShowRequest";

    private SnipsDatabase? _database;
    private ForegroundWindowTracker? _foregroundTracker;
    private HotKeyManager? _hotKeyManager;
    private MainWindow? _mainWindow;
    private Mutex? _singleInstanceMutex;
    private EventWaitHandle? _showRequestEvent;

    // Registration ids for the CURRENT set of per-snippet hotkeys, so a later re-registration
    // (see RegisterPerSnippetHotkeysAsync) can cleanly unregister the old set before adding the
    // new one, rather than leaking stale WM_HOTKEY registrations behind a since-changed shortcut.
    private readonly List<int> _perSnippetHotkeyIds = [];

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Safe baseline before the real, persisted language is known (that needs the database,
        // opened a few lines down) — guarantees UiStrings.Get() always resolves to real English
        // text rather than a raw resource key, even for an exception this early. Upgraded to the
        // user's actual language below once it's available.
        UiStrings.Apply("en");

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
                UiStrings.Get("Str_UnhandledExceptionFormat", ex.Exception.Message),
                UiStrings.Get("Str_AppName"), MessageBoxButton.OK, MessageBoxImage.Warning);
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

        var languageCode = await _database.Settings.GetAsync("Language") ?? "en";

        // Must happen before any window is constructed — every XAML string reference is
        // {DynamicResource Str_X}, resolved against Application.Resources at the moment each
        // control is realized, so the right dictionary needs to already be merged in.
        UiStrings.Apply(languageCode);

        await SeedExampleSnippetsIfEmptyAsync(_database, languageCode);
        await LanguagePackSeedData.SeedIfEmptyAsync(_database.VariableTranslations);

        // Must exist before the window so WM_HOTKEY delivery (via SetWinEventHook,
        // WINEVENT_OUTOFCONTEXT) starts as soon as the Dispatcher message loop is pumping.
        _foregroundTracker = new ForegroundWindowTracker();

        var externalVariablesPath = DatabasePathResolver.ResolveExternalVariablesPath(dbPath);
        _mainWindow = new MainWindow(_database, _foregroundTracker, externalVariablesPath, RegisterPerSnippetHotkeysAsync);
        _hotKeyManager = new HotKeyManager(_mainWindow);

        var boundLabel = RegisterFirstAvailableHotkey();

        _mainWindow.TrayIcon.ToolTipText = boundLabel is null
            ? UiStrings.Get("Str_TrayTooltipNoHotkeyFormat", BuildIdentifier.Value)
            : UiStrings.Get("Str_TrayTooltipWithHotkeyFormat", BuildIdentifier.Value, boundLabel);

        if (boundLabel is null)
        {
            MessageBox.Show(
                UiStrings.Get("Str_AllHotkeysClaimedFormat", string.Join(", ", HotkeyCandidates.Select(FormatCandidateLabel))),
                UiStrings.Get("Str_AppName"), MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else if (boundLabel != FormatCandidateLabel(HotkeyCandidates[0]) && await _database.Settings.GetAsync("HotkeyFallbackNoticeShown") is null)
        {
            // Shown once ever, not on every launch — this was genuinely nagging Roland during
            // repeated dev-cycle relaunches and reads as "the app is broken" rather than as the
            // one-time heads-up it's meant to be.
            MessageBox.Show(
                UiStrings.Get("Str_HotkeyFallbackNoticeFormat", boundLabel),
                UiStrings.Get("Str_AppName"), MessageBoxButton.OK, MessageBoxImage.Information);
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
    /// list (as SPEC.md §5.8 describes) is not built yet; this only surfaces failures here.
    ///
    /// Also passed into MainWindow as a callback and re-run any time a shortcut is assigned or
    /// cleared from the UI (the editor's Assign/Clear, or the list's "Define a shortcut…") — a
    /// per-snippet hotkey previously only got its real WM_HOTKEY registration at app startup, so
    /// a shortcut assigned during a running session looked saved (it was, in the database) but
    /// silently never actually fired until the app was next restarted. Unregistering the
    /// previous set first avoids leaking a stale registration behind a since-changed shortcut.
    /// </summary>
    private async Task RegisterPerSnippetHotkeysAsync()
    {
        foreach (var id in _perSnippetHotkeyIds)
            _hotKeyManager!.Unregister(id);
        _perSnippetHotkeyIds.Clear();

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
            else
            {
                _perSnippetHotkeyIds.Add(id.Value);
            }
        }

        if (failedSnippetNames.Count > 0)
        {
            MessageBox.Show(
                UiStrings.Get("Str_PerSnippetHotkeyFailedFormat", string.Join(", ", failedSnippetNames)),
                UiStrings.Get("Str_AppName"), MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private string? RegisterFirstAvailableHotkey()
    {
        foreach (var candidate in HotkeyCandidates)
        {
            var id = _hotKeyManager!.Register(candidate.Modifiers, candidate.VirtualKey, _mainWindow!.ShowAndFocus);
            if (id is not null)
                return FormatCandidateLabel(candidate);
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
    ///
    /// Translated into German/French/Italian too — unlike the app's own UI chrome, this is
    /// snippet CONTENT (the same category as anything a user could type themselves), but for a
    /// first-run "gift" experience the very first thing a new recipient sees shouldn't be
    /// permanently English just because the rest of the app is localized. Uses the plain English
    /// master variable keys in every language ({{date}}, {{user}}, ...) rather than assuming any
    /// particular variable-name translation is already populated — those always work regardless
    /// of what the recipient later customizes in Settings -&gt; "Manage translations…".
    /// </summary>
    private static async Task SeedExampleSnippetsIfEmptyAsync(SnipsDatabase database, string languageCode)
    {
        if ((await database.Snippets.ListAsync()).Count > 0)
            return;

        var examples = ExampleSnippetsByLanguage.GetValueOrDefault(languageCode, ExampleSnippetsByLanguage["en"]);

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

    private static readonly Dictionary<string, (string Name, string Description, string Body)[]> ExampleSnippetsByLanguage = new()
    {
        ["en"] =
        [
            ("Meeting follow-up", "Sent after a client call",
                "Dear {{input:Name}},\n\nThank you for your time today, {{date}}. I'll follow up " +
                "with next steps by {{date:+3d:dd.MM.yyyy}}.\n\nBest regards,\n{{user}}"),
            ("Email signature", "Quick plain-text signature",
                "{{user}}\n{{useremail}}\n{{date}}"),
            ("Bug report template", "Starting point for a bug report",
                "Environment: {{os}} ({{osversion}})\nReported by: {{user}} on {{date}}\n\n" +
                "Steps to reproduce:\n1. \n2. \n\nExpected:\nActual:"),
            ("Quick timestamp", "Paste the current date and time", "{{datetime}}"),
        ],
        ["de"] =
        [
            ("Nachfassen nach einem Meeting", "Wird nach einem Kundengespräch verschickt",
                "Liebe/r {{input:Name}},\n\nVielen Dank für Ihre Zeit heute, {{date}}. Ich melde mich " +
                "mit den nächsten Schritten bis {{date:+3d:dd.MM.yyyy}}.\n\nFreundliche Grüsse\n{{user}}"),
            ("E-Mail-Signatur", "Kurze Klartext-Signatur",
                "{{user}}\n{{useremail}}\n{{date}}"),
            ("Fehlerbericht-Vorlage", "Ausgangspunkt für einen Fehlerbericht",
                "Umgebung: {{os}} ({{osversion}})\nGemeldet von: {{user}} am {{date}}\n\n" +
                "Schritte zur Reproduktion:\n1. \n2. \n\nErwartet:\nTatsächlich:"),
            ("Schneller Zeitstempel", "Fügt das aktuelle Datum und die Uhrzeit ein", "{{datetime}}"),
        ],
        ["fr"] =
        [
            ("Suivi de réunion", "Envoyé après un appel client",
                "Cher/Chère {{input:Name}},\n\nMerci pour votre temps aujourd'hui, {{date}}. Je reviendrai " +
                "vers vous avec les prochaines étapes d'ici le {{date:+3d:dd.MM.yyyy}}.\n\nCordialement,\n{{user}}"),
            ("Signature e-mail", "Signature rapide en texte brut",
                "{{user}}\n{{useremail}}\n{{date}}"),
            ("Modèle de rapport de bug", "Point de départ pour un rapport de bug",
                "Environnement : {{os}} ({{osversion}})\nSignalé par : {{user}} le {{date}}\n\n" +
                "Étapes pour reproduire :\n1. \n2. \n\nAttendu :\nRéel :"),
            ("Horodatage rapide", "Colle la date et l'heure actuelles", "{{datetime}}"),
        ],
        ["it"] =
        [
            ("Follow-up riunione", "Inviato dopo una chiamata con il cliente",
                "Gentile {{input:Name}},\n\nGrazie per il tempo dedicato oggi, {{date}}. Vi aggiornerò sui " +
                "prossimi passi entro il {{date:+3d:dd.MM.yyyy}}.\n\nCordiali saluti,\n{{user}}"),
            ("Firma e-mail", "Firma rapida in testo semplice",
                "{{user}}\n{{useremail}}\n{{date}}"),
            ("Modello segnalazione bug", "Punto di partenza per una segnalazione di bug",
                "Ambiente: {{os}} ({{osversion}})\nSegnalato da: {{user}} il {{date}}\n\n" +
                "Passaggi per riprodurre:\n1. \n2. \n\nAtteso:\nEffettivo:"),
            ("Timestamp rapido", "Incolla la data e l'ora correnti", "{{datetime}}"),
        ],
    };
}
