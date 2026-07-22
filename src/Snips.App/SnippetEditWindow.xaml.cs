using System.Windows;

namespace Snips.App;

public partial class SnippetEditWindow : Wpf.Ui.Controls.FluentWindow
{
    public string EnteredName => NameBox.Text.Trim();
    public string EnteredDescription => DescriptionBox.Text.Trim();
    public string EnteredBody => BodyBox.Text;

    public SnippetEditWindow(string? name = null, string? description = null, string? body = null)
    {
        InitializeComponent();
        NameBox.Text = name ?? string.Empty;
        DescriptionBox.Text = description ?? string.Empty;
        BodyBox.Text = body ?? string.Empty;
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
}
