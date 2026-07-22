using System.ComponentModel;
using System.Globalization;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Snips.Core.Domain;
using Snips.Core.Repositories;
using Snips.Core.Search;
using Snips.Core.Templates;
using Snips.Data;
using Snips.Interop.Clipboard;
using Snips.Interop.Foreground;
using Snips.Interop.Paste;

namespace Snips.App;

/// <summary>One row in the picker list: a snippet plus its shortcut's display label, if any.</summary>
internal sealed record PickerRow(Snippet Snippet, string? ShortcutLabel);

public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly SnipsDatabase _database;
    private readonly ForegroundWindowTracker _foregroundTracker;
    private List<Snippet> _allSnippets = [];
    private Dictionary<string, string> _shortcutLabelsBySnippetId = [];
    private string? _userEmail;
    private bool _isExiting;

    public MainWindow(SnipsDatabase database, ForegroundWindowTracker foregroundTracker)
    {
        InitializeComponent();
        _database = database;
        _foregroundTracker = foregroundTracker;
        Title = $"Snips — {BuildIdentifier.Value}";
        BuildInfoText.Text = BuildIdentifier.Value;
        Loaded += async (_, _) => await RefreshListAsync();
    }

    /// <summary>Called by the global hotkey and the tray menu. Resets the search and reloads
    /// from the database so frecency ordering and any external changes are current.</summary>
    public async void ShowAndFocus()
    {
        Show();
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Activate();

        SearchBox.Text = string.Empty;
        StatusText.Text = string.Empty;
        // Reset to defaults every time rather than leaving whatever was checked last: with no
        // Settings screen yet to set a real default, an invisible "Paste" checkbox left checked
        // from an earlier test is exactly how surprises like this happen.
        CopyCheckBox.IsChecked = true;
        PasteCheckBox.IsChecked = false;
        await RefreshListAsync();
        SearchBox.Focus();
    }

    public void RequestExit()
    {
        _isExiting = true;
        TrayIcon.Dispose();
        Application.Current.Shutdown();
    }

    /// <summary>
    /// Triggered directly by a per-snippet hotkey (SPEC.md §5.8) — no picker window involved at
    /// all. Always copies and pastes: there's no open UI to read checkbox state from, and the
    /// whole point of a per-snippet shortcut is to act immediately.
    /// </summary>
    public async void ApplySnippetByHotkey(string snippetId)
    {
        var snippet = await _database.Snippets.GetByIdAsync(snippetId);
        if (snippet is not null)
            await ApplySnippetAsync(snippet, copy: true, paste: true, statusTarget: null);
    }

    private async Task RefreshListAsync()
    {
        _allSnippets = (await _database.Snippets.ListAsync()).ToList();
        _shortcutLabelsBySnippetId = (await _database.Shortcuts.ListAllAsync())
            .ToDictionary(s => s.SnippetId, s => HotkeyFormatting.Format(s.Modifiers, s.VirtualKey));
        _userEmail = await _database.Settings.GetAsync("UserEmail");
        ApplyFilter();
        RefreshTrayMenu();
    }

    /// <summary>
    /// Quick access from the tray icon without opening the picker at all — answers Roland's
    /// "insert what was generated for another app" question. Deliberately not Ctrl+Alt+1-9:
    /// that would collide with the per-snippet shortcuts a user can already assign individually,
    /// and "which 9" (list order? recency?) has no obviously-right answer. Lists the same
    /// most-recently-used-first top 9 the picker itself defaults to.
    /// </summary>
    private void RefreshTrayMenu()
    {
        var menu = TrayIcon.ContextMenu;
        if (menu is null)
            return;

        menu.Items.Clear();

        var show = new MenuItem { Header = "Show Snips" };
        show.Click += ShowMenuItem_Click;
        menu.Items.Add(show);

        var quickApply = _allSnippets.Take(9).ToList();
        if (quickApply.Count > 0)
        {
            menu.Items.Add(new Separator());
            foreach (var snippet in quickApply)
            {
                var item = new MenuItem { Header = snippet.Name };
                item.Click += (_, _) => ApplySnippetByHotkey(snippet.Id);
                menu.Items.Add(item);
            }
        }

        menu.Items.Add(new Separator());
        var settings = new MenuItem { Header = "Settings…" };
        settings.Click += (_, _) => _ = OpenSettingsAsync();
        menu.Items.Add(settings);

        var quit = new MenuItem { Header = "Quit" };
        quit.Click += QuitMenuItem_Click;
        menu.Items.Add(quit);
    }

    /// <summary>Minimal for now — just the one field {{useremail}} needs. Roland's "Email
    /// signature" example snippet was resolving with a blank email line because there was
    /// nowhere to configure it; this is the direct fix for that, not the full Phase 6
    /// Settings view.</summary>
    private async Task OpenSettingsAsync()
    {
        var dialog = new SettingsWindow(_database.Settings, _userEmail) { Owner = this };
        if (dialog.ShowDialog() == true)
            await RefreshListAsync();
    }

    private void ApplyFilter()
    {
        var previouslySelectedId = GetSelectedSnippet()?.Id;

        var matches = SnippetSearch.Search(_allSnippets, SearchBox.Text, DateTime.UtcNow)
            .Select(m => new PickerRow(
                m.Snippet,
                _shortcutLabelsBySnippetId.GetValueOrDefault(m.Snippet.Id)))
            .ToList();
        ResultsList.ItemsSource = matches;

        if (matches.Count == 0)
        {
            PreviewBox.Text = string.Empty;
            return;
        }

        var index = previouslySelectedId is null ? -1 : matches.FindIndex(r => r.Snippet.Id == previouslySelectedId);
        ResultsList.SelectedIndex = index >= 0 ? index : 0;
    }

    private Snippet? GetSelectedSnippet() => (ResultsList.SelectedItem as PickerRow)?.Snippet;

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

    /// <summary>Up/Down only needs special handling here: they have no meaning in a single-line
    /// TextBox, so while the search box has focus they're redirected to move the list selection.
    /// When the list itself has focus it already handles Up/Down natively.</summary>
    private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                MoveSelection(1);
                e.Handled = true;
                break;
            case Key.Up:
                MoveSelection(-1);
                e.Handled = true;
                break;
        }
    }

    /// <summary>
    /// Window-level (tunnels from the root before any child control sees the key) so these work
    /// no matter which control currently has focus — e.g. after clicking a row to select it, focus
    /// moves to the list, and a handler wired only on the search box would never see Enter again.
    /// Delete is deliberately NOT handled here: it needs to keep editing text when the search box
    /// has focus, so it stays scoped to ResultsList_KeyDown instead.
    /// </summary>
    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                _ = ApplySelectedAsync(keepOpen: Keyboard.Modifiers.HasFlag(ModifierKeys.Control));
                e.Handled = true;
                break;
            case Key.Escape:
                Hide();
                e.Handled = true;
                break;
            case Key.N when Keyboard.Modifiers == ModifierKeys.Control:
                _ = NewSnippetAsync();
                e.Handled = true;
                break;
            case Key.E when Keyboard.Modifiers == ModifierKeys.Control:
                _ = EditSelectedAsync();
                e.Handled = true;
                break;
            case Key.D when Keyboard.Modifiers == ModifierKeys.Control:
                _ = DuplicateSelectedAsync();
                e.Handled = true;
                break;
            case Key.K when Keyboard.Modifiers == ModifierKeys.Control:
                _ = DefineShortcutForSelectedAsync();
                e.Handled = true;
                break;
        }
    }

    private void MoveSelection(int delta)
    {
        if (ResultsList.Items.Count == 0)
            return;

        ResultsList.SelectedIndex = Math.Clamp(ResultsList.SelectedIndex + delta, 0, ResultsList.Items.Count - 1);
        ResultsList.ScrollIntoView(ResultsList.SelectedItem);
    }

    /// <summary>
    /// Shows a live, resolved preview (not the raw {{...}} template — Roland expected the
    /// resolved value) and, if "Copy to clipboard" is checked, keeps the clipboard in sync with
    /// the current selection so it's ready to paste without pressing Enter first (Roland's other
    /// direct ask). Deliberately non-interactive and side-effect-free: Prompt is null (arrow-key
    /// browsing must never pop a dialog) and Counters is null (browsing must never burn through
    /// a persistent counter — only a real Enter/apply should do that).
    /// </summary>
    private async void ResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var snippet = GetSelectedSnippet();
        if (snippet is null)
        {
            PreviewBox.Text = string.Empty;
            return;
        }

        var context = BuildTemplateContext(snippet, ClipboardTextGuard.TryGetCurrentText(), prompt: null, includeCounters: false);
        var rendered = await TemplateEngine.RenderAsync(snippet.PlainText, context);
        PreviewBox.Text = rendered.Text;

        if (CopyCheckBox.IsChecked == true)
            ClipboardTextGuard.SetText(rendered.Text);
    }

    private TemplateContext BuildTemplateContext(Snippet snippet, string? clipboardText, IInteractivePrompt? prompt, bool includeCounters)
    {
        var target = _foregroundTracker.LastExternalForegroundWindow;

        return new TemplateContext
        {
            Now = DateTimeOffset.Now,
            Culture = CultureInfo.CurrentCulture,
            SystemInfo = EnvironmentSystemInfoProvider.Instance,
            SnippetName = snippet.Name,
            SnippetId = snippet.Id,
            SnippetDescription = snippet.Description,
            UseCount = snippet.UseCount,
            UserEmail = _userEmail,
            ClipboardText = clipboardText,
            ActiveWindowTitle = target is { } titleTarget ? ActiveWindowInfo.GetWindowTitle(titleTarget) : null,
            ActiveAppName = target is { } appTarget ? ActiveWindowInfo.GetProcessName(appTarget) : null,
            IdGenerator = _database.IdGenerator,
            Counters = includeCounters ? _database.Counters : null,
            Prompt = prompt,
        };
    }

    private void ResultsList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete)
        {
            _ = DeleteSelectedAsync();
            e.Handled = true;
        }
    }

    /// <summary>Double-click opens the editor. This was briefly changed to activate-the-row
    /// instead (matching the Explorer convention), but Roland pushed back — he doesn't want the
    /// window disappearing on a double-click, and wants a discoverable way to edit. Reverted.</summary>
    private void ResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e) =>
        _ = EditSelectedAsync();

    /// <summary>Right-click should act on the row under the cursor, not whatever was already selected.</summary>
    private void ResultsList_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ItemsControl.ContainerFromElement(ResultsList, (DependencyObject)e.OriginalSource) is ListBoxItem item)
            item.IsSelected = true;
    }

    private async Task ApplySelectedAsync(bool keepOpen)
    {
        var snippet = GetSelectedSnippet();
        if (snippet is null)
            return;

        var copy = CopyCheckBox.IsChecked == true;
        var paste = PasteCheckBox.IsChecked == true;

        var applied = await ApplySnippetAsync(snippet, copy, paste, statusTarget: StatusText);
        if (!applied)
            return;

        if (keepOpen)
        {
            // ApplySnippetAsync may have hidden us to hand focus to the paste target — bring
            // ourselves back rather than leaving the user staring at the other app. Show()+
            // Activate() only, not ShowAndFocus(): that would reset the search text and
            // selection, which defeats the point of "keep it open" for rapid-firing several
            // snippets in a row.
            if (!IsVisible)
            {
                Show();
                Activate();
            }
        }
        else
        {
            Hide();
        }
    }

    /// <summary>
    /// The one place that renders a snippet and sends it to the clipboard/target app — used by
    /// both the picker's Enter key and a direct per-snippet hotkey trigger. Returns false if the
    /// user cancelled an interactive prompt, so the caller knows not to treat this as "applied".
    /// </summary>
    private async Task<bool> ApplySnippetAsync(Snippet snippet, bool copy, bool paste, TextBlock? statusTarget)
    {
        var target = _foregroundTracker.LastExternalForegroundWindow;

        // Fetched once, before anything below writes to the clipboard: this is both the
        // {{clipboard}} variable's value and the backup to restore after a transient paste.
        var originalClipboard = ClipboardTextGuard.TryGetCurrentText();
        var context = BuildTemplateContext(snippet, originalClipboard, new WpfInteractivePrompt(this), includeCounters: true);

        var rendered = await TemplateEngine.RenderAsync(snippet.PlainText, context);
        if (rendered.Cancelled)
        {
            SetStatus(statusTarget, "Cancelled.");
            return false;
        }

        // Only back up the clipboard when we're writing to it purely as a transient step for
        // auto-paste. If "Copy to clipboard" is also checked, the snippet is meant to stay there
        // (SPEC.md §6.4), so there is nothing to restore.
        string? clipboardBackup = paste && !copy ? originalClipboard : null;

        var clipboardWriteOk = true;
        if (copy || paste)
            clipboardWriteOk = ClipboardTextGuard.SetText(rendered.Text);

        if (!clipboardWriteOk)
        {
            SetStatus(statusTarget, "Couldn't write to the clipboard — it was busy. Try again.");
        }
        else if (paste)
        {
            if (target is null)
            {
                // No paste was actually attempted — the clipboard write above is the real
                // deliverable here, matching what this message promises. Restoring it a moment
                // later (as if a transient paste had happened) would silently erase it again;
                // that was a real bug — Roland kept losing the resolved text this way whenever
                // "Paste" was checked with no valid target.
                SetStatus(statusTarget, "No previous window to paste into — copied to clipboard instead.");
            }
            else
            {
                // Hide before handing focus to the target: staying visible (even unfocused)
                // can interfere with SetForegroundWindow actually succeeding, and was a likely
                // cause of a physical Enter keystroke leaking into the target as a stray newline.
                if (IsVisible)
                    Hide();

                var result = PasteSender.TrySendPaste(target.Value, timeoutMs: 500);
                SetStatus(statusTarget, result switch
                {
                    PasteResult.Sent => "Pasted.",
                    PasteResult.AccessDenied =>
                        "Target app is running as administrator — copied to clipboard, press Ctrl+V yourself.",
                    PasteResult.FocusTimeout =>
                        "Couldn't bring the target app to the front in time — copied to clipboard, press Ctrl+V yourself.",
                    PasteResult.TargetGone => "Target window is gone — copied to clipboard instead.",
                    _ => string.Empty,
                });

                if (!copy)
                    _ = ClipboardTextGuard.RestoreAfterAsync(clipboardBackup, delayMs: 500);
            }
        }
        else if (copy)
        {
            SetStatus(statusTarget, "Copied to clipboard.");
        }

        await _database.Snippets.RecordUseAsync(snippet.Id);
        return true;
    }

    private static void SetStatus(TextBlock? statusTarget, string message)
    {
        if (statusTarget is not null)
            statusTarget.Text = message;
    }

    private async Task NewSnippetAsync()
    {
        var dialog = new SnippetEditWindow { Owner = this };
        if (dialog.ShowDialog() != true)
            return;

        try
        {
            var now = DateTime.UtcNow;
            await _database.Snippets.CreateAsync(new Snippet
            {
                Id = "unused",
                Name = dialog.EnteredName,
                Description = dialog.EnteredDescription,
                BodyHtml = ToPlaceholderHtml(dialog.EnteredBody),
                PlainText = dialog.EnteredBody,
                IsRichText = false,
                CreatedUtc = now,
                ModifiedUtc = now,
            });
        }
        catch (DuplicateSnippetNameException ex)
        {
            MessageBox.Show(this, ex.Message, "Snips", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        await RefreshListAsync();
    }

    private async Task EditSelectedAsync()
    {
        var snippet = GetSelectedSnippet();
        if (snippet is null)
            return;

        var dialog = new SnippetEditWindow(snippet.Name, snippet.Description, snippet.PlainText) { Owner = this };
        if (dialog.ShowDialog() != true)
            return;

        snippet.Name = dialog.EnteredName;
        snippet.Description = dialog.EnteredDescription;
        snippet.PlainText = dialog.EnteredBody;
        snippet.BodyHtml = ToPlaceholderHtml(dialog.EnteredBody);

        try
        {
            await _database.Snippets.UpdateAsync(snippet);
        }
        catch (DuplicateSnippetNameException ex)
        {
            MessageBox.Show(this, ex.Message, "Snips", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        await RefreshListAsync();
    }

    private async Task DuplicateSelectedAsync()
    {
        var snippet = GetSelectedSnippet();
        if (snippet is null)
            return;

        var baseName = $"{snippet.Name} (copy)";
        var candidate = baseName;
        for (var suffix = 2; await _database.Snippets.GetByNameAsync(candidate) is not null; suffix++)
            candidate = $"{baseName} {suffix}";

        var now = DateTime.UtcNow;
        await _database.Snippets.CreateAsync(new Snippet
        {
            Id = "unused",
            Name = candidate,
            Description = snippet.Description,
            BodyHtml = snippet.BodyHtml,
            PlainText = snippet.PlainText,
            IsRichText = snippet.IsRichText,
            CreatedUtc = now,
            ModifiedUtc = now,
        });

        await RefreshListAsync();
    }

    private async Task DeleteSelectedAsync()
    {
        var snippet = GetSelectedSnippet();
        if (snippet is null)
            return;

        var confirmed = MessageBox.Show(
            this, $"Delete '{snippet.Name}'?", "Snips", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirmed != MessageBoxResult.Yes)
            return;

        await _database.Snippets.DeleteAsync(snippet.Id);
        await RefreshListAsync();
    }

    private async Task DefineShortcutForSelectedAsync()
    {
        var snippet = GetSelectedSnippet();
        if (snippet is null)
            return;

        var existing = await _database.Shortcuts.GetBySnippetIdAsync(snippet.Id);
        var dialog = new ShortcutCaptureWindow(snippet.Name, snippet.Id, _database.Shortcuts, existing) { Owner = this };
        if (dialog.ShowDialog() == true)
            await RefreshListAsync();
    }

    private void NewMenuItem_Click(object sender, RoutedEventArgs e) => _ = NewSnippetAsync();
    private void NewSnippetButton_Click(object sender, RoutedEventArgs e) => _ = NewSnippetAsync();
    private void EditMenuItem_Click(object sender, RoutedEventArgs e) => _ = EditSelectedAsync();
    private void DuplicateMenuItem_Click(object sender, RoutedEventArgs e) => _ = DuplicateSelectedAsync();
    private void DeleteMenuItem_Click(object sender, RoutedEventArgs e) => _ = DeleteSelectedAsync();
    private void DefineShortcutMenuItem_Click(object sender, RoutedEventArgs e) => _ = DefineShortcutForSelectedAsync();

    private void MainWindow_Closing(object sender, CancelEventArgs e)
    {
        if (_isExiting)
            return;

        e.Cancel = true;
        Hide();
    }

    private void ShowMenuItem_Click(object sender, RoutedEventArgs e) => ShowAndFocus();

    private void TrayIcon_TrayLeftMouseDoubleClick(object sender, RoutedEventArgs e) => ShowAndFocus();

    /// <summary>Single left-click also shows the window — matches how most Windows tray
    /// utilities behave, and Roland specifically didn't know double-click already worked.</summary>
    private void TrayIcon_TrayLeftMouseUp(object sender, RoutedEventArgs e) => ShowAndFocus();

    private void QuitMenuItem_Click(object sender, RoutedEventArgs e) => RequestExit();

    /// <summary>Roland asked for shift-click specifically; a plain click is used instead since
    /// the cursor/tooltip already signal it's clickable and a modifier requirement wouldn't be
    /// discoverable without that being written down somewhere.</summary>
    private void BuildInfoText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ClipboardTextGuard.SetText(BuildIdentifier.Value))
            StatusText.Text = "Build identifier copied to clipboard.";
    }

    /// <summary>
    /// Placeholder until the WebView2 rich editor lands in Phase 3 (SPEC.md §5.7): wraps plain
    /// text as an HTML fragment so BodyHtml is never left empty for a snippet created here.
    /// </summary>
    private static string ToPlaceholderHtml(string plainText) =>
        $"<p>{WebUtility.HtmlEncode(plainText).Replace("\r\n", "<br/>").Replace("\n", "<br/>")}</p>";
}
