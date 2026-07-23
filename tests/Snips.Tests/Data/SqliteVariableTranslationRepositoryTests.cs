using Snips.Core.Repositories;

namespace Snips.Tests.Data;

public class SqliteVariableTranslationRepositoryTests : IAsyncLifetime
{
    private readonly TempDatabaseFixture _fixture = new();
    private IVariableTranslationRepository Translations => _fixture.Database.VariableTranslations;

    public Task InitializeAsync() => _fixture.InitializeAsync();
    public Task DisposeAsync() => _fixture.DisposeAsync();

    [Fact]
    public async Task AddOrUpdateLanguageAsync_ThenListLanguagesAsync_ReturnsIt()
    {
        await Translations.AddOrUpdateLanguageAsync("de-CH", "Deutsch (Schweiz)", isRightToLeft: false);

        var languages = await Translations.ListLanguagesAsync();

        var german = Assert.Single(languages);
        Assert.Equal("de-CH", german.Code);
        Assert.Equal("Deutsch (Schweiz)", german.DisplayName);
        Assert.False(german.IsRightToLeft);
    }

    [Fact]
    public async Task AddOrUpdateLanguageAsync_CalledAgain_UpdatesRatherThanDuplicates()
    {
        await Translations.AddOrUpdateLanguageAsync("ar", "Arabic (draft)", isRightToLeft: false);
        await Translations.AddOrUpdateLanguageAsync("ar", "العربية", isRightToLeft: true);

        var languages = await Translations.ListLanguagesAsync();

        var arabic = Assert.Single(languages);
        Assert.Equal("العربية", arabic.DisplayName);
        Assert.True(arabic.IsRightToLeft);
    }

    [Fact]
    public async Task SetAsync_ThenListTranslationsAsync_ReturnsIt()
    {
        await Translations.AddOrUpdateLanguageAsync("de-CH", "Deutsch", false);

        await Translations.SetAsync("date", "de-CH", "heute");

        var translations = await Translations.ListTranslationsAsync("de-CH");
        var entry = Assert.Single(translations);
        Assert.Equal("date", entry.MasterKey);
        Assert.Equal("heute", entry.LocalName);
    }

    [Fact]
    public async Task SetAsync_CalledAgainForSameMasterKey_OverwritesRatherThanDuplicates()
    {
        await Translations.AddOrUpdateLanguageAsync("de-CH", "Deutsch", false);
        await Translations.SetAsync("date", "de-CH", "heute");

        await Translations.SetAsync("date", "de-CH", "datum"); // changed their mind

        var translations = await Translations.ListTranslationsAsync("de-CH");
        var entry = Assert.Single(translations);
        Assert.Equal("datum", entry.LocalName);
    }

    [Fact]
    public async Task SetAsync_LocalNameAlreadyUsedByAnotherMasterKeyInSameLanguage_Throws()
    {
        await Translations.AddOrUpdateLanguageAsync("de-CH", "Deutsch", false);
        await Translations.SetAsync("date", "de-CH", "heute");

        await Assert.ThrowsAsync<DuplicateVariableTranslationException>(
            () => Translations.SetAsync("localdate", "de-CH", "heute"));
    }

    [Fact]
    public async Task SetAsync_SameLocalNameInDifferentLanguages_BothAllowed()
    {
        await Translations.AddOrUpdateLanguageAsync("de-CH", "Deutsch", false);
        await Translations.AddOrUpdateLanguageAsync("de-DE", "Deutsch (Deutschland)", false);

        await Translations.SetAsync("date", "de-CH", "heute");
        await Translations.SetAsync("date", "de-DE", "heute");

        Assert.Single(await Translations.ListTranslationsAsync("de-CH"));
        Assert.Single(await Translations.ListTranslationsAsync("de-DE"));
    }

    [Fact]
    public async Task RemoveAsync_DeletesTheTranslation()
    {
        await Translations.AddOrUpdateLanguageAsync("de-CH", "Deutsch", false);
        await Translations.SetAsync("date", "de-CH", "heute");

        await Translations.RemoveAsync("date", "de-CH");

        Assert.Empty(await Translations.ListTranslationsAsync("de-CH"));
    }

    [Fact]
    public async Task DeletingTheLanguage_CascadesToItsTranslations()
    {
        await Translations.AddOrUpdateLanguageAsync("de-CH", "Deutsch", false);
        await Translations.SetAsync("date", "de-CH", "heute");

        // No RemoveLanguageAsync on the interface yet (not needed by anything built so far) —
        // exercise the cascade via a second connection to the same file, to prove the FK
        // constraint itself works, so a future RemoveLanguageAsync can rely on it.
        await using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_fixture.DatabasePath}"))
        {
            await connection.OpenAsync();
            await using var pragma = connection.CreateCommand();
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            await pragma.ExecuteNonQueryAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Language WHERE Code = 'de-CH';";
            await command.ExecuteNonQueryAsync();
        }

        Assert.Empty(await Translations.ListTranslationsAsync("de-CH"));
    }
}
