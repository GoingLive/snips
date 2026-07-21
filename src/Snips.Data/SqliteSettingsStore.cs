using Microsoft.Data.Sqlite;
using Snips.Core.Repositories;

namespace Snips.Data;

public sealed class SqliteSettingsStore(SqliteConnection connection) : ISettingsStore
{
    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM Setting WHERE Key = $key;";
        command.Parameters.AddWithValue("$key", key);
        var result = await command.ExecuteScalarAsync(ct);
        return result as string;
    }

    public async Task SetAsync(string key, string value, CancellationToken ct = default)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Setting (Key, Value) VALUES ($key, $value)
            ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        await command.ExecuteNonQueryAsync(ct);
    }
}
