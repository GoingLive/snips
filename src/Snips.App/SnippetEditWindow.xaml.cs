using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Snips.Core.Templates;

namespace Snips.App;

internal sealed record VariableReferenceItem(string Token, string Description);

public partial class SnippetEditWindow : Wpf.Ui.Controls.FluentWindow
{
    /// <summary>A curated subset of variables where a worked example (an offset, a custom
    /// format, an argument) is more useful than the bare name alone. Anything not listed here
    /// still shows up via BuiltInVariableCatalog.All below, so a newly added variable is always
    /// discoverable even before anyone gets around to giving it a nicer example.</summary>
    private static readonly VariableReferenceItem[] CuratedExamples =
    [
        new("{{date:dd.MM.yyyy}}", "Today's date, custom format — any format may contain ':' freely"),
        new("{{date:+7d:dd.MM.yyyy}}", "Date with an offset and a custom format together"),
        new("{{now:yyyy-MM-dd HH:mm}}", "Current date/time in any custom format you choose"),
        new("{{random:1-100}}", "A random number in a range"),
        new("{{randomstring:12}}", "A random alphanumeric string of the given length"),
        new("{{counter:Invoice}}", "A persistent counter — increments every time this snippet is used"),
        new("{{input:Name}}", "Prompts for a value called Name"),
        new("{{input:Name:Default}}", "Prompts for a value, pre-filled with a default"),
        new("{{multiline:Notes}}", "Prompts for multi-line text"),
        new("{{choice:Size:S,M,L}}", "Prompts to pick one of the listed options"),
        new("{{datepick:When:yyyy-MM-dd}}", "Prompts to pick a date"),
        new("{{check:Confirmed:yes,no}}", "Prompts for a checkbox"),
        new("{{clipboard|upper}}", "Filters chain with | — this uppercases the clipboard text"),
    ];

    /// <summary>The built-in variables from SPEC.md §7, shown so users can see the real
    /// supported names/syntax instead of guessing (e.g. {{Roland}}, {{DD.MM.YYYY}} — neither is
    /// a real variable, so both were correctly left as literal text by the template engine).
    /// Built from BuiltInVariableCatalog.All (the same master list BuiltInVariables.cs's switch
    /// statement and the translation system are checked against) plus CuratedExamples above, so
    /// a variable added to the catalog can never silently go missing here again — it previously
    /// drifted by ~20 entries because this list was hand-maintained separately.</summary>
    private static readonly VariableReferenceItem[] VariableReference = BuildVariableReference();

    private static VariableReferenceItem[] BuildVariableReference()
    {
        var curatedNames = CuratedExamples
            .Select(item => item.Token.Trim('{', '}').Split(':')[0].Split('|')[0])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var fromCatalog = BuiltInVariableCatalog.All
            .Where(v => v.Name != "selection" && !curatedNames.Contains(v.Name))
            .Select(v => new VariableReferenceItem($"{{{{{v.Name}}}}}", v.Description));

        return [.. CuratedExamples, .. fromCatalog];
    }

    public string EnteredName => NameBox.Text.Trim();
    public string EnteredDescription => DescriptionBox.Text.Trim();
    public string EnteredBody => BodyBox.Text;
    public bool EnteredIsFavorite => FavoriteCheckBox.IsChecked == true;

    /// <summary>Set when the user confirms Delete. Distinct from DialogResult (which Close()
    /// without an explicit assignment defaults to false) so the caller can tell "cancelled"
    /// apart from "delete this" even though both leave ShowDialog() returning false/null.</summary>
    public bool DeleteRequested { get; private set; }

    public SnippetEditWindow(string? name = null, string? description = null, string? body = null, bool isFavorite = false)
    {
        InitializeComponent();
        NameBox.Text = name ?? string.Empty;
        DescriptionBox.Text = description ?? string.Empty;
        BodyBox.Text = body ?? string.Empty;
        FavoriteCheckBox.IsChecked = isFavorite;
        VariableReferenceList.ItemsSource = VariableReference;
        // Only an existing snippet (opened via Edit) can be deleted — New passes no name.
        DeleteButton.Visibility = name is not null ? Visibility.Visible : Visibility.Collapsed;
        Loaded += (_, _) => NameBox.Focus();
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        var confirmed = MessageBox.Show(
            this, $"Delete '{EnteredName}'?", "Snips", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirmed != MessageBoxResult.Yes)
            return;

        DeleteRequested = true;
        Close();
    }

    private void VariableSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var query = VariableSearchBox.Text.Trim();
        VariableReferenceList.ItemsSource = query.Length == 0
            ? VariableReference
            : VariableReference.Where(v =>
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
            ShowError("Name is required.");
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
