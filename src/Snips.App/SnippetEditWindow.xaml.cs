using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Snips.Core.Domain;
using Snips.Core.Repositories;
using Snips.Core.Templates;

namespace Snips.App;

internal sealed record VariableReferenceItem(string Token, string Description);

public partial class SnippetEditWindow : Wpf.Ui.Controls.FluentWindow
{
    /// <summary>A curated subset of variables where a worked example (an offset, a custom
    /// format, an argument) is more useful than the bare name alone. Anything not listed here
    /// still shows up via BuiltInVariableCatalog.All below, so a newly added variable is always
    /// discoverable even before anyone gets around to giving it a nicer example.
    ///
    /// Built per-instance (not a static field) specifically so its descriptions — hardcoded here
    /// in Snips.App rather than pulled from BuiltInVariableCatalog in Snips.Core, so the existing
    /// VariableNameTranslation system never covered them — pick up UiStrings.Get() at whatever
    /// language was active the moment THIS editor window opened, rather than being frozen at
    /// whatever language happened to be active the first time any editor was ever constructed.</summary>
    private static VariableReferenceItem[] BuildCuratedExamples() =>
    [
        new("{{date:dd.MM.yyyy}}", UiStrings.Get("Str_Example_DateFormat")),
        new("{{date:+7d:dd.MM.yyyy}}", UiStrings.Get("Str_Example_DateOffsetFormat")),
        new("{{now:yyyy-MM-dd HH:mm}}", UiStrings.Get("Str_Example_NowFormat")),
        new("{{random:1-100}}", UiStrings.Get("Str_Example_Random")),
        new("{{randomstring:12}}", UiStrings.Get("Str_Example_RandomString")),
        new("{{counter:Invoice}}", UiStrings.Get("Str_Example_Counter")),
        new("{{input:Name}}", UiStrings.Get("Str_Example_Input")),
        new("{{input:Name:Default}}", UiStrings.Get("Str_Example_InputDefault")),
        new("{{multiline:Notes}}", UiStrings.Get("Str_Example_Multiline")),
        new("{{choice:Size:S,M,L}}", UiStrings.Get("Str_Example_Choice")),
        new("{{datepick:When:yyyy-MM-dd}}", UiStrings.Get("Str_Example_Datepick")),
        new("{{check:Confirmed:yes,no}}", UiStrings.Get("Str_Example_Check")),
        new("{{clipboard|upper}}", UiStrings.Get("Str_Example_FilterChain")),
    ];

    /// <summary>The built-in variables from SPEC.md §7, shown so users can see the real
    /// supported names/syntax instead of guessing (e.g. {{Roland}}, {{DD.MM.YYYY}} — neither is
    /// a real variable, so both were correctly left as literal text by the template engine).
    /// Built from BuiltInVariableCatalog.All (the same master list BuiltInVariables.cs's switch
    /// statement and the translation system are checked against) plus the curated examples above,
    /// so a variable added to the catalog can never silently go missing here again — it
    /// previously drifted by ~20 entries because this list was hand-maintained separately.</summary>
    private static VariableReferenceItem[] BuildVariableReference()
    {
        var curatedExamples = BuildCuratedExamples();
        var curatedNames = curatedExamples
            .Select(item => item.Token.Trim('{', '}').Split(':')[0].Split('|')[0])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var fromCatalog = BuiltInVariableCatalog.All
            .Where(v => v.Name != "selection" && !curatedNames.Contains(v.Name))
            .Select(v => new VariableReferenceItem($"{{{{{v.Name}}}}}", v.Description));

        return [.. curatedExamples, .. fromCatalog];
    }

    private readonly IShortcutRepository? _shortcuts;
    private readonly string? _snippetId;
    private readonly VariableReferenceItem[] _variableReference;
    private Shortcut? _currentShortcut;

    public string EnteredName => NameBox.Text.Trim();
    public string EnteredDescription => DescriptionBox.Text.Trim();
    public string EnteredBody => BodyBox.Text;
    public bool EnteredIsFavorite => FavoriteCheckBox.IsChecked == true;

    /// <summary>Set when the user confirms Delete. Distinct from DialogResult (which Close()
    /// without an explicit assignment defaults to false) so the caller can tell "cancelled"
    /// apart from "delete this" even though both leave ShowDialog() returning false/null.</summary>
    public bool DeleteRequested { get; private set; }

    /// <summary>
    /// shortcuts/snippetId are omitted for a brand-new snippet (no ID exists yet to attach a
    /// shortcut to — see the Shortcut row's own comment in the XAML) and required to make the
    /// row's Assign/Clear buttons functional when editing an existing one.
    /// </summary>
    public SnippetEditWindow(
        string? name = null, string? description = null, string? body = null, bool isFavorite = false,
        IShortcutRepository? shortcuts = null, string? snippetId = null, Shortcut? existingShortcut = null)
    {
        InitializeComponent();
        NameBox.Text = name ?? string.Empty;
        DescriptionBox.Text = description ?? string.Empty;
        BodyBox.Text = body ?? string.Empty;
        FavoriteCheckBox.IsChecked = isFavorite;
        _variableReference = BuildVariableReference();
        VariableReferenceList.ItemsSource = _variableReference;
        // Only an existing snippet (opened via Edit) can be deleted — New passes no name.
        DeleteButton.Visibility = name is not null ? Visibility.Visible : Visibility.Collapsed;

        _shortcuts = shortcuts;
        _snippetId = snippetId;
        _currentShortcut = existingShortcut;
        AssignShortcutButton.IsEnabled = snippetId is not null;
        AssignShortcutButton.ToolTip = snippetId is null
            ? UiStrings.Get("Str_AssignTooltipDisabled")
            : null;
        UpdateShortcutDisplay();

        Loaded += (_, _) => NameBox.Focus();
    }

    private void UpdateShortcutDisplay()
    {
        ShortcutDisplayText.Text = _currentShortcut is null
            ? UiStrings.Get("Str_None")
            : HotkeyFormatting.Format(_currentShortcut.Modifiers, _currentShortcut.VirtualKey);
        ClearShortcutButton.Visibility = _currentShortcut is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void AssignShortcutButton_Click(object sender, RoutedEventArgs e)
    {
        if (_shortcuts is null || _snippetId is null)
            return;

        var dialog = new ShortcutCaptureWindow(EnteredName, _snippetId, _shortcuts, _currentShortcut) { Owner = this };
        if (dialog.ShowDialog() != true)
            return;

        _currentShortcut = await _shortcuts.GetBySnippetIdAsync(_snippetId);
        UpdateShortcutDisplay();
    }

    private async void ClearShortcutButton_Click(object sender, RoutedEventArgs e)
    {
        if (_shortcuts is null || _snippetId is null)
            return;

        await _shortcuts.RemoveAsync(_snippetId);
        _currentShortcut = null;
        UpdateShortcutDisplay();
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        var confirmed = MessageBox.Show(
            this, UiStrings.Get("Str_DeleteConfirmFormat", EnteredName), UiStrings.Get("Str_AppName"),
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirmed != MessageBoxResult.Yes)
            return;

        DeleteRequested = true;
        Close();
    }

    private void VariableSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var query = VariableSearchBox.Text.Trim();
        VariableReferenceList.ItemsSource = query.Length == 0
            ? _variableReference
            : _variableReference.Where(v =>
                v.Token.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                v.Description.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(EnteredName))
        {
            ShowError(UiStrings.Get("Str_NameRequiredError"));
            return;
        }

        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void VariableReferenceList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => InsertSelectedToken();

    private void VariableReferenceList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            InsertSelectedToken();
            e.Handled = true;
        }
    }

    private void InsertSelectedToken()
    {
        if (VariableReferenceList.SelectedItem is not VariableReferenceItem item)
            return;

        var caret = BodyBox.CaretIndex;
        BodyBox.Text = BodyBox.Text.Insert(caret, item.Token);
        BodyBox.CaretIndex = caret + item.Token.Length;
        BodyBox.Focus();
    }
}
