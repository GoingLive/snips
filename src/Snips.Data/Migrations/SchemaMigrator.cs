using Microsoft.Data.Sqlite;

namespace Snips.Data.Migrations;

internal static class SchemaMigrator
{
    /// <summary>Returns the versions actually applied, in order. Empty if the schema was already current.</summary>
    public static async Task<IReadOnlyList<int>> MigrateAsync(SqliteConnection connection, CancellationToken ct = default)
    {
        await using (var createVersionTable = connection.CreateCommand())
        {
            createVersionTable.CommandText = """
                CREATE TABLE IF NOT EXISTS SchemaVersion (
                    Version    INTEGER NOT NULL,
                    AppliedUtc TEXT    NOT NULL
                );
                """;
            await createVersionTable.ExecuteNonQueryAsync(ct);
        }

        var currentVersion = await GetCurrentVersionAsync(connection, ct);
        var pending = MigrationCatalog.All.Where(m => m.Version > currentVersion).OrderBy(m => m.Version).ToList();
        var applied = new List<int>();

        foreach (var migration in pending)
        {
            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(ct);

            await using (var apply = connection.CreateCommand())
            {
                apply.Transaction = transaction;
                apply.CommandText = migration.Sql;
                await apply.ExecuteNonQueryAsync(ct);
            }

            await using (var recordVersion = connection.CreateCommand())
            {
                recordVersion.Transaction = transaction;
                recordVersion.CommandText = "INSERT INTO SchemaVersion (Version, AppliedUtc) VALUES ($version, $appliedUtc);";
                recordVersion.Parameters.AddWithValue("$version", migration.Version);
                recordVersion.Parameters.AddWithValue("$appliedUtc", DateTime.UtcNow.ToString("O"));
                await recordVersion.ExecuteNonQueryAsync(ct);
            }

            await transaction.CommitAsync(ct);
            applied.Add(migration.Version);
        }

        return applied;
    }

    public static async Task<int> GetCurrentVersionAsync(SqliteConnection connection, CancellationToken ct = default)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COALESCE(MAX(Version), 0) FROM SchemaVersion;";
        var result = await command.ExecuteScalarAsync(ct);
        return Convert.ToInt32(result);
    }

    public static bool HasPendingMigrations(int currentVersion) =>
        MigrationCatalog.All.Any(m => m.Version > currentVersion);

    public static int LatestVersion => MigrationCatalog.All.Max(m => m.Version);
}
