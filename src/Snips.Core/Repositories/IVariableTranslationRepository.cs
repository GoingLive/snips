using Snips.Core.Domain;

namespace Snips.Core.Repositories;

public interface IVariableTranslationRepository
{
    Task<IReadOnlyList<Language>> ListLanguagesAsync(CancellationToken ct = default);

    /// <summary>Creates the language if it doesn't exist yet, or updates DisplayName/IsRightToLeft
    /// if it does — languages themselves aren't versioned, just upserted.</summary>
    Task<Language> AddOrUpdateLanguageAsync(string code, string displayName, bool isRightToLeft, CancellationToken ct = default);

    Task<IReadOnlyList<VariableNameTranslation>> ListTranslationsAsync(string languageCode, CancellationToken ct = default);

    /// <summary>Creates or overwrites the translation for (languageCode, masterKey). Throws
    /// <see cref="DuplicateVariableTranslationException"/> if localName is already taken by a
    /// different masterKey in that language.</summary>
    Task<VariableNameTranslation> SetAsync(string masterKey, string languageCode, string localName, CancellationToken ct = default);

    Task RemoveAsync(string masterKey, string languageCode, CancellationToken ct = default);
}
