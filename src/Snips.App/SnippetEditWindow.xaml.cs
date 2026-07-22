using System.Windows;
using System.Windows.Input;

namespace Snips.App;

internal sealed record VariableReferenceItem(string Token, string Description);

public partial class SnippetEditWindow : Wpf.Ui.Controls.FluentWindow
{
    /// <summary>The built-in variables from SPEC.md §7, shown so users can see the real
    /// supported names/syntax instead of guessing (e.g. {{Roland}}, {{DD.MM.YYYY}} — neither
    /// is a real variable, so both were correctly left as literal text by the template engine).</summary>
    private static readonly VariableReferenceItem[] VariableReference =
    [
        new("{{date}}", "Today's date (yyyy-MM-dd)"),
        new("{{date:dd.MM.yyyy}}", "Today's date, custom format"),
        new("{{time}}", "Current time (HH:mm:ss)"),
        new("{{datetime}}", "Date and time together"),
        new("{{year}}", "Current year"),
        new("{{weekday}}", "Day name, e.g. Tuesday"),
        new("{{tomorrow}}", "Tomorrow's date"),
        new("{{yesterday}}", "Yesterday's date"),
        new("{{user}}", "Your Windows login name"),
        new("{{useremail}}", "Your email — needs a Settings screen to configure, not built yet"),
        new("{{machine}}", "Computer name"),
        new("{{clipboard}}", "Current clipboard text"),
        new("{{snippetname}}", "This snippet's own name"),
        new("{{usecount}}", "Times this snippet has been used"),
        new("{{guid}}", "A random unique ID"),
        new("{{random:1-100}}", "A random number in a range"),
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

    public SnippetEditWindow(string? name = null, string? description = null, string? body = null)
    {
        InitializeComponent();
        NameBox.Text = name ?? string.Empty;
        DescriptionBox.Text = description ?? string.Empty;
        BodyBox.Text = body ?? string.Empty;
        VariableReferenceList.ItemsSource = VariableReference;
        Loaded += (_, _) => NameBox.Focus();
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
