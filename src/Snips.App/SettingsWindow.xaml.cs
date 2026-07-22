using System.Windows;
using Snips.Core.Repositories;

namespace Snips.App;

public partial class SettingsWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly ISettingsStore _settings;

    public SettingsWindow(ISettingsStore settings, string? currentEmail)
    {
        InitializeComponent();
        _settings = settings;
        EmailBox.Text = currentEmail ?? string.Empty;
        Loaded += (_, _) => EmailBox.Focus();
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        await _settings.SetAsync("UserEmail", EmailBox.Text.Trim());
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
