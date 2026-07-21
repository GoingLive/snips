using Microsoft.Data.Sqlite;
using Snips.Core.Templates;

namespace Snips.Data;

public sealed class SqliteCounterStore(SqliteConnection connection) : ICounterStore
{
    public async Task<long> IncrementAndGetAsync(string name, long step, CancellationToken ct = default)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Counter (Name, Value) VALUES ($name, $step)
            ON CONFLICT(Name) DO UPDATE SET Value = Value + excluded.Value
            RETURNING Value;
            """;
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$step", step);

        var result = await command.ExecuteScalarAsync(ct);
        return Convert.ToInt64(result);
    }
}
