using Microsoft.Data.Sqlite;
using Snips.Data;

namespace Snips.Tests.Data;

public class SnipsDatabaseTests : IAsyncLifetime
{
    private readonly TempDatabaseFixture _fixture = new();

    public Task InitializeAsync() => _fixture.InitializeAsync();
    public Task DisposeAsync() => _fixture.DisposeAsync();

    [Fact]
    public async Task OpenAsync_CreatesAllTablesFromSchema()
    {
        var expectedTables = new[]
        {
            "Snippet", "SnippetAsset", "Shortcut", "Variable", "Folder", "Tag", "SnippetTag",
            "Counter", "Setting", "SchemaVersion", "Language", "VariableNameTranslation",
        };

        var actualTables = await GetTableNamesAsync(_fixture.DatabasePath);

        foreach (var table in expectedTables)
            Assert.Contains(table, actualTables);
    }

    // MigrationCatalog.All.Count as of this test: 3 (InitialSchema, LanguagePackPhase1,
    // FavoriteSortOrder). MigrationCatalog is internal with no InternalsVisibleTo, so this is a
    // hand-kept number, not a reflected one — bump it whenever a migration is added.
    private const int ExpectedMigrationCount = 3;

    [Fact]
    public async Task OpenAsync_RecordsSchemaVersionExactlyOnce()
    {
        Assert.Equal(ExpectedMigrationCount, await CountSchemaVersionRowsAsync(_fixture.DatabasePath));
    }

    [Fact]
    public async Task OpenAsync_ReopeningAnExistingDatabase_DoesNotReapplyMigrations()
    {
        await using (var second = await SnipsDatabase.OpenAsync(_fixture.DatabasePath))
        {
            // Opening again against the same file must not re-run any migration.
        }

        Assert.Equal(ExpectedMigrationCount, await CountSchemaVersionRowsAsync(_fixture.DatabasePath));
    }

    [Fact]
    public async Task OpenAsync_PersistsTheSameInstanceIdAcrossReopens()
    {
        var firstInstanceId = _fixture.Database.IdGenerator.NextId();

        await using var second = await SnipsDatabase.OpenAsync(_fixture.DatabasePath);
        var secondInstanceId = await second.Settings.GetAsync("InstanceId");
        var firstInstanceIdSetting = await _fixture.Database.Settings.GetAsync("InstanceId");

        Assert.NotNull(secondInstanceId);
        Assert.Equal(firstInstanceIdSetting, secondInstanceId);
        Assert.NotEmpty(firstInstanceId);
    }

    private static async Task<long> CountSchemaVersionRowsAsync(string path)
    {
        await using var connection = new SqliteConnection($"Data Source={path}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM SchemaVersion;";
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<List<string>> GetTableNamesAsync(string path)
    {
        await using var connection = new SqliteConnection($"Data Source={path}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table';";

        var names = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            names.Add(reader.GetString(0));

        return names;
    }
}
