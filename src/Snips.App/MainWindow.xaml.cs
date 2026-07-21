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

public partial class MainWindow : Window
{
    private readonly SnipsDatabase _database;
    private readonly ForegroundWindowTracker _foregroundTracker;
    private List<Snippet> _allSnippets = [];
    private bool _isExiting;

    public MainWindow(SnipsDatabase database, ForegroundWindowTracker foregroundTracker)
    {
        InitializeComponent();
        _database = database;
        _foregroundTracker = foregroundTracker;
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
        await RefreshListAsync();
        SearchBox.Focus();
    }

    public void RequestExit()
    {
        _isExiting = true;
        TrayIcon.Dispose();
        Application.Current.Shutdown();
    }

    private async Task RefreshListAsync()
    {
        _allSnippets = (await _database.Snippets.ListAsync()).ToList();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var previouslySelectedId = (ResultsList.SelectedItem as Snippet)?.Id;

        var matches = SnippetSearch.Search(_allSnippets, SearchBox.Text, DateTime.UtcNow)
            .Select(m => m.Snippet)
            .ToList();
        ResultsList.ItemsSource = matches;

        if (matches.Count == 0)
        {
            PreviewBox.Text = string.Empty;
            return;
        }

        var index = previouslySelectedId is null ? -1 : matches.FindIndex(s => s.Id == previouslySelectedId);
        ResultsList.SelectedIndex = index >= 0 ? index : 0;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

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
        }
    }

    private void MoveSelection(int delta)
    {
        if (ResultsList.Items.Count == 0)
            return;

        ResultsList.SelectedIndex = Math.Clamp(ResultsList.SelectedIndex + delta, 0, ResultsList.Items.Count - 1);
        ResultsList.ScrollIntoView(ResultsList.SelectedItem);
    }

    private void ResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        PreviewBox.Text = (ResultsList.SelectedItem as Snippet)?.PlainText ?? string.Empty;
    }

    private void ResultsList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete)
        {
            _ = DeleteSelectedAsync();
            e.Handled = true;
        }
    }

    private void ResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => _ = EditSelectedAsync();

    private async Task ApplySelectedAsync(bool keepOpen)
    {
        if (ResultsList.SelectedItem is not Snippet snippet)
            return;

        var copy = CopyCheckBox.IsChecked == true;
        var paste = PasteCheckBox.IsChecked == true;
        var target = _foregroundTracker.LastExternalForegroundWindow;

        // Fetched once, before anything below writes to the clipboard: this is both the
        // {{clipboard}} variable's value and the backup to restore after a transient paste.
        var originalClipboard = ClipboardTextGuard.TryGetCurrentText();

        var context = new TemplateContext
        {
            Now = DateTimeOffset.Now,
            Culture = CultureInfo.CurrentCulture,
            SystemInfo = EnvironmentSystemInfoProvider.Instance,
            SnippetName = snippet.Name,
            SnippetId = snippet.Id,
            SnippetDescription = snippet.Description,
            UseCount = snippet.UseCount,
            ClipboardText = originalClipboard,
            ActiveWindowTitle = target is { } titleTarget ? ActiveWindowInfo.GetWindowTitle(titleTarget) : null,
            ActiveAppName = target is { } appTarget ? ActiveWindowInfo.GetProcessName(appTarget) : null,
            IdGenerator = _database.IdGenerator,
            Counters = _database.Counters,
            Prompt = new WpfInteractivePrompt(this),
        };

        var rendered = await TemplateEngine.RenderAsync(snippet.PlainText, context);
        if (rendered.Cancelled)
        {
            StatusText.Text = "Cancelled.";
            return;
        }

        // Only back up the clipboard when we're writing to it purely as a transient step for
        // auto-paste. If "Copy to clipboard" is also checked, the snippet is meant to stay there
        // (SPEC.md §6.4), so there is nothing to restore.
        string? clipboardBackup = paste && !copy ? originalClipboard : null;

        if (copy || paste)
            ClipboardTextGuard.SetText(rendered.Text);

        if (paste)
        {
            if (target is null)
            {
                StatusText.Text = "No previous window to paste into — copied to clipboard instead.";
            }
            else
            {
                var result = PasteSender.TrySendPaste(target.Value, delayMs: 60);
                StatusText.Text = result switch
                {
                    PasteResult.Sent => "Pasted.",
                    PasteResult.AccessDenied =>
                        "Target app is running as administrator — copied to clipboard, press Ctrl+V yourself.",
                    PasteResult.TargetGone => "Target window is gone — copied to clipboard instead.",
                    _ => string.Empty,
                };
            }

            if (!copy)
                _ = ClipboardTextGuard.RestoreAfterAsync(clipboardBackup, delayMs: 500);
        }
        else if (copy)
        {
            StatusText.Text = "Copied to clipboard.";
        }

        await _database.Snippets.RecordUseAsync(snippet.Id);

        if (!keepOpen)
            Hide();
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
        if (ResultsList.SelectedItem is not Snippet snippet)
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
        if (ResultsList.SelectedItem is not Snippet snippet)
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
        if (ResultsList.SelectedItem is not Snippet snippet)
            return;

        var confirmed = MessageBox.Show(
            this, $"Delete '{snippet.Name}'?", "Snips", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirmed != MessageBoxResult.Yes)
            return;

        await _database.Snippets.DeleteAsync(snippet.Id);
        await RefreshListAsync();
    }

    private void MainWindow_Closing(object sender, CancelEventArgs e)
    {
        if (_isExiting)
            return;

        e.Cancel = true;
        Hide();
    }

    private void ShowMenuItem_Click(object sender, RoutedEventArgs e) => ShowAndFocus();

    private void TrayIcon_TrayLeftMouseDoubleClick(object sender, RoutedEventArgs e) => ShowAndFocus();

    private void QuitMenuItem_Click(object sender, RoutedEventArgs e) => RequestExit();

    /// <summary>
    /// Placeholder until the WebView2 rich editor lands in Phase 3 (SPEC.md §5.7): wraps plain
    /// text as an HTML fragment so BodyHtml is never left empty for a snippet created here.
    /// </summary>
    private static string ToPlaceholderHtml(string plainText) =>
        $"<p>{WebUtility.HtmlEncode(plainText).Replace("\r\n", "<br/>").Replace("\n", "<br/>")}</p>";
}
