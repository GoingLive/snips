namespace Snips.Core.Domain;

/// <summary>One entry in the fixed language picker — see docs/language-pack-brief.md's target
/// list. Distinct from the Language DB table: English isn't a row there (it IS the master key
/// set, nothing to translate it to), but it's still a real, selectable choice here.</summary>
public sealed record SupportedLanguage(string Code, string DisplayName);

public static class SupportedLanguages
{
    public static readonly IReadOnlyList<SupportedLanguage> All =
    [
        new("en", "English"),
        new("de", "Deutsch"),
        new("fr", "Français"),
        new("it", "Italiano"),
        new("es", "Español"),
        new("ru", "Русский"),
        new("zh", "中文"),
        new("ar", "العربية"),
    ];
}
