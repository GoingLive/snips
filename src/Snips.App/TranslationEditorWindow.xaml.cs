using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Snips.Core.Domain;
using Snips.Core.Repositories;
using Snips.Core.Templates;

namespace Snips.App;

/// <summary>One grid row: an English master key, its meaning, and whatever local name the
/// admin has typed for the currently-selected language. INotifyPropertyChanged so the
/// missing-translation row highlight (bound to HasTranslation) updates live while typing,
/// not just after a save/reload.</summary>
internal sealed class TranslationRow(string masterKey, string description, string originalLocalName) : INotifyPropertyChanged
{
    private string _localName = originalLocalName;

    public string MasterKey { get; } = masterKey;
    public string Description { get; } = description;
    public string OriginalLocalName { get; private set; } = originalLocalName;

    public string LocalName
    {
        get => _localName;
        set
        {
            if (_localName == value)
                return;

            _localName = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LocalName)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasTranslation)));
        }
    }

    public bool HasTranslation => !string.IsNullOrWhiteSpace(LocalName);
    public bool Changed => LocalName.Trim() != OriginalLocalName.Trim();

    public void MarkSaved() => OriginalLocalName = LocalName;

    public event PropertyChangedEventHandler? PropertyChanged;
}

public partial class TranslationEditorWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly IVariableTranslationRepository _translations;
    private List<TranslationRow> _allRows = [];

    public TranslationEditorWindow(IVariableTranslationRepository translations)
    {
        InitializeComponent();
        _translations = translations;

        // English excluded on purpose — it IS the master key set, there's nothing to translate
        // it to (see BuiltInVariableCatalog's own doc comment).
        LanguageComboBox.ItemsSource = SupportedLanguages.All.Where(l => l.Code != "en").ToList();
        LanguageComboBox.SelectedIndex = 0;
    }

    private async void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        await LoadRowsForSelectedLanguageAsync();

    private async Task LoadRowsForSelectedLanguageAsync()
    {
        if (LanguageComboBox.SelectedItem is not SupportedLanguage language)
            return;

        var existing = await _translations.ListTranslationsAsync(language.Code);
        var byMasterKey = existing.ToDictionary(t => t.MasterKey, t => t.LocalName, StringComparer.OrdinalIgnoreCase);

        _allRows = BuiltInVariableCatalog.All
            .Select(v => new TranslationRow(v.Name, v.Description, byMasterKey.GetValueOrDefault(v.Name, "")))
            .ToList();

        ApplyFilter();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

    private void ApplyFilter()
    {
        var query = SearchBox.Text.Trim();
        TranslationGrid.ItemsSource = query.Length == 0
            ? _allRows
            : _allRows.Where(r =>
                r.MasterKey.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                r.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                r.LocalName.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        UpdateCoverageText();
    }

    private void UpdateCoverageText()
    {
        var translated = _allRows.Count(r => r.HasTranslation);
        CoverageText.Text = $"{translated} of {_allRows.Count} translated.";
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (LanguageComboBox.SelectedItem is not SupportedLanguage language)
            return;

        // Upsert the Language row itself every save — cheap, and means picking a language here
        // for the first time is enough to make it exist, no separate "register a language" step.
        await _translations.AddOrUpdateLanguageAsync(language.Code, language.DisplayName, isRightToLeft: language.Code == "ar");

        foreach (var row in _allRows.Where(r => r.Changed))
        {
            if (row.HasTranslation)
                await _translations.SetAsync(row.MasterKey, language.Code, row.LocalName.Trim());
            else
                await _translations.RemoveAsync(row.MasterKey, language.Code);

            row.MarkSaved();
        }

        UpdateCoverageText();
        CoverageText.Text += " Saved.";
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
