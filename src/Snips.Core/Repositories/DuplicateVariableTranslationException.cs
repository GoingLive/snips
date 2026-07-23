namespace Snips.Core.Repositories;

/// <summary>Thrown when a LocalName is already used by a different variable in the same
/// language (IX_VarTranslation_LangLocalName) — one name can't mean two things in one language.</summary>
public sealed class DuplicateVariableTranslationException(string localName, string languageCode)
    : Exception($"'{localName}' is already used by another variable in {languageCode}.")
{
    public string LocalName { get; } = localName;
    public string LanguageCode { get; } = languageCode;
}
