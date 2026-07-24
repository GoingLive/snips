using System.Windows;
using Snips.Core.Domain;
using Snips.Core.Repositories;

namespace Snips.App;

public partial class SettingsWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly ISettingsStore _settings;
    private readonly IVariableTranslationRepository _translations;

    public SettingsWindow(ISettingsStore settings, IVariableTranslationRepository translations, string? currentEmail, string currentLanguageCode)
    {
        InitializeComponent();
        _settings = settings;
        _translations = translations;
        EmailBox.Text = currentEmail ?? string.Empty;

        LanguageComboBox.ItemsSource = SupportedLanguages.All;
        LanguageComboBox.SelectedItem = SupportedLanguages.All.FirstOrDefault(l =>
            string.Equals(l.Code, currentLanguageCode, StringComparison.OrdinalIgnoreCase)) ?? SupportedLanguages.All[0];

        Loaded += (_, _) => EmailBox.Focus();
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        await _settings.SetAsync("UserEmail", EmailBox.Text.Trim());

        var selectedLanguage = (SupportedLanguage)LanguageComboBox.SelectedItem;
        await _settings.SetAsync("Language", selectedLanguage.Code);

        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void ManageTranslationsButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new TranslationEditorWindow(_translations) { Owner = this };
        dialog.ShowDialog();
    }
}
