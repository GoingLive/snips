using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Snips.App;

internal sealed record VariableReferenceItem(string Token, string Description);

public partial class SnippetEditWindow : Wpf.Ui.Controls.FluentWindow
{
    /// <summary>The built-in variables from SPEC.md §7 / docs/variables.yaml, shown so users
    /// can see the real supported names/syntax instead of guessing (e.g. {{Roland}},
    /// {{DD.MM.YYYY}} — neither is a real variable, so both were correctly left as literal text
    /// by the template engine). Kept in sync by hand with BuiltInVariables.cs — there's no
    /// reflection-based generation, so a newly added variable needs an entry here too.</summary>
    private static readonly VariableReferenceItem[] VariableReference =
    [
        new("{{date}}", "Today's date (yyyy-MM-dd)"),
        new("{{date:dd.MM.yyyy}}", "Today's date, custom format — any format may contain ':' freely"),
        new("{{date:+7d:dd.MM.yyyy}}", "Date with an offset and a custom format together"),
        new("{{time}}", "Current time (HH:mm:ss)"),
        new("{{datetime}}", "Date and time together"),
        new("{{iso}}", "ISO 8601 date/time with UTC offset"),
        new("{{localdate}}", "Short date in your Windows display-language format"),
        new("{{localtime}}", "Short time in your Windows display-language format"),
        new("{{locallongdate}}", "Long date in your Windows display-language format"),
        new("{{locallongtime}}", "Long time in your Windows display-language format"),
        new("{{intldate}}", "Spelled-out English date, e.g. \"23 July 2026\", regardless of locale"),
        new("{{now:yyyy-MM-dd HH:mm}}", "Current date/time in any custom format you choose"),
        new("{{year}}", "Current year"),
        new("{{weekday}}", "Day name, e.g. Tuesday"),
        new("{{monthname}}", "Month name, e.g. July"),
        new("{{week}}", "ISO-8601 week number"),
        new("{{quarter}}", "Current quarter, e.g. Q3"),
        new("{{tomorrow}}", "Tomorrow's date"),
        new("{{yesterday}}", "Yesterday's date"),
        new("{{timezone}}", "Your Windows time zone ID"),
        new("{{snipsversion}}", "The build of Snips currently running"),
        new("{{user}}", "Your Windows login name"),
        new("{{userfullname}}", "Your Windows full display name"),
        new("{{useremail}}", "Your email — set it in the tray menu's Settings…"),
        new("{{machine}}", "Computer name"),
        new("{{os}}", "Operating system name"),
        new("{{home}}", "Your user home folder path"),
        new("{{clipboard}}", "Current clipboard text"),
        new("{{activewindow}}", "Title of the window Snips will paste into"),
        new("{{activeapp}}", "Name of the app Snips will paste into"),
        new("{{snippetname}}", "This snippet's own name"),
        new("{{usecount}}", "Times this snippet has been used"),
        new("{{guid}}", "A random unique ID"),
        new("{{id}}", "A locally-unique, sortable ID (Snowflake)"),
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
