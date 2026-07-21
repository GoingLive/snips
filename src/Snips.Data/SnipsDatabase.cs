using Microsoft.Data.Sqlite;
using Snips.Core.Id;
using Snips.Core.Repositories;
using Snips.Core.Templates;
using Snips.Data.Migrations;
using Snips.Data.Repositories;

namespace Snips.Data;

/// <summary>
/// Owns the single long-lived connection to a snips.db file (WAL mode suits one writer / many
/// readers in a single-user desktop app better than short-lived pooled connections), runs
/// migrations on open, and exposes the repositories. See SPEC.md §4.4 for path selection rules.
/// </summary>
public sealed class SnipsDatabase : IAsyncDisposable
{
    private const string InstanceIdSettingKey = "InstanceId";

    private readonly SqliteConnection _connection;

    public ISnippetRepository Snippets { get; }
    public ISettingsStore Settings { get; }
    public ICounterStore Counters { get; }
    public SnowflakeIdGenerator IdGenerator { get; }

    private SnipsDatabase(SqliteConnection connection, SnowflakeIdGenerator idGenerator)
    {
        _connection = connection;
        IdGenerator = idGenerator;
        Settings = new SqliteSettingsStore(connection);
        Snippets = new SqliteSnippetRepository(connection, idGenerator);
        Counters = new SqliteCounterStore(connection);
    }

    public static async Task<SnipsDatabase> OpenAsync(string path, CancellationToken ct = default)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var existedBefore = File.Exists(path);

        var connection = new SqliteConnection($"Data Source={path}");
        await connection.OpenAsync(ct);
        await ApplyPragmasAsync(connection, ct);

        if (existedBefore)
        {
            var currentVersion = await SchemaMigrator.GetCurrentVersionAsync(connection, ct);
            if (SchemaMigrator.HasPendingMigrations(currentVersion))
                await BackupBeforeMigratingAsync(connection, path, SchemaMigrator.LatestVersion, ct);
        }

        await SchemaMigrator.MigrateAsync(connection, ct);

        var instanceId = await GetOrCreateInstanceIdAsync(connection, ct);
        var idGenerator = new SnowflakeIdGenerator(instanceId);

        return new SnipsDatabase(connection, idGenerator);
    }

    private static async Task ApplyPragmasAsync(SqliteConnection connection, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode = WAL; PRAGMA foreign_keys = ON;";
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task BackupBeforeMigratingAsync(
        SqliteConnection connection, string path, int targetVersion, CancellationToken ct)
    {
        var backupPath = $"{path}.backup-{targetVersion}-{DateTime.UtcNow:yyyyMMddHHmmss}";
        await using var backupConnection = new SqliteConnection($"Data Source={backupPath}");
        await backupConnection.OpenAsync(ct);
        connection.BackupDatabase(backupConnection);
    }

    private static async Task<long> GetOrCreateInstanceIdAsync(SqliteConnection connection, CancellationToken ct)
    {
        var store = new SqliteSettingsStore(connection);
        var existing = await store.GetAsync(InstanceIdSettingKey, ct);
        if (existing is not null && long.TryParse(existing, out var parsed))
            return parsed;

        var generated = Random.Shared.NextInt64(0, 1024);
        await store.SetAsync(InstanceIdSettingKey, generated.ToString(), ct);
        return generated;
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}
