using System.Windows;

namespace Snips.App;

/// <summary>
/// Loads/swaps the app's UI-chrome string dictionary (Resources/Strings/Strings.*.xaml) — the
/// window titles, button labels, tooltips, and status/error messages a user reads, as distinct
/// from the separate, already-existing VariableNameTranslation system that only translates the
/// {{variable}} tokens themselves inside a snippet's own body.
///
/// XAML references a string via {DynamicResource Str_Foo}; code-behind (MessageBox bodies,
/// dynamically-built status text, the tray menu) calls <see cref="Get"/>. Both read from the
/// same merged dictionaries, so there is exactly one source of truth per string regardless of
/// which side references it.
/// </summary>
internal static class UiStrings
{
    private const string EnglishSource = "/Snips.App;component/Resources/Strings/Strings.en.xaml";

    private static readonly Dictionary<string, string> SourceByLanguage = new()
    {
        ["de"] = "/Snips.App;component/Resources/Strings/Strings.de.xaml",
        ["fr"] = "/Snips.App;component/Resources/Strings/Strings.fr.xaml",
        ["it"] = "/Snips.App;component/Resources/Strings/Strings.it.xaml",
    };

    /// <summary>
    /// Swaps the active UI-string dictionary. English is always merged first as the fallback
    /// layer (a key missing from a translation resolves to its English text instead of throwing
    /// or showing the raw resource key — the same "degrade, don't fail" principle the template
    /// engine uses), with the target language's dictionary merged on top so its entries win.
    /// Every currently open window updates immediately: every XAML reference here uses
    /// {DynamicResource}, not {StaticResource}, specifically so a language change from Settings
    /// doesn't need a restart to take effect.
    /// </summary>
    public static void Apply(string languageCode)
    {
        var dictionaries = Application.Current.Resources.MergedDictionaries;

        for (var i = dictionaries.Count - 1; i >= 0; i--)
        {
            var source = dictionaries[i].Source?.OriginalString;
            if (source is not null && source.Contains("/Resources/Strings/"))
                dictionaries.RemoveAt(i);
        }

        dictionaries.Add(new ResourceDictionary { Source = new Uri(EnglishSource, UriKind.Relative) });
        if (SourceByLanguage.TryGetValue(languageCode, out var translatedSource))
            dictionaries.Add(new ResourceDictionary { Source = new Uri(translatedSource, UriKind.Relative) });
    }

    public static string Get(string key) => Application.Current.TryFindResource(key) as string ?? key;

    public static string Get(string key, params object[] args) => string.Format(Get(key), args);
}
