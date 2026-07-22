using Microsoft.Data.Sqlite;
using Snips.Core.Domain;
using Snips.Core.Id;
using Snips.Core.Repositories;

namespace Snips.Data.Repositories;

public sealed class SqliteShortcutRepository(SqliteConnection connection, SnowflakeIdGenerator idGenerator)
    : IShortcutRepository
{
    private const int SqliteConstraintUnique = 2067; // SQLITE_CONSTRAINT_UNIQUE
    private const string Columns = "Id, SnippetId, Modifiers, VirtualKey, IsEnabled";

    public async Task<Shortcut?> GetBySnippetIdAsync(string snippetId, CancellationToken ct = default)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {Columns} FROM Shortcut WHERE SnippetId = $snippetId;";
        command.Parameters.AddWithValue("$snippetId", snippetId);

        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadShortcut(reader) : null;
    }

    public async Task<IReadOnlyList<Shortcut>> ListAllAsync(CancellationToken ct = default)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {Columns} FROM Shortcut;";

        var results = new List<Shortcut>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            results.Add(ReadShortcut(reader));

        return results;
    }

    public async Task<Shortcut> SetAsync(string snippetId, int modifiers, int virtualKey, CancellationToken ct = default)
    {
        var existing = await GetBySnippetIdAsync(snippetId, ct);
        var shortcut = new Shortcut
        {
            Id = existing?.Id ?? idGenerator.NextId(),
            SnippetId = snippetId,
            Modifiers = modifiers,
            VirtualKey = virtualKey,
            IsEnabled = true,
        };

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Shortcut (Id, SnippetId, Modifiers, VirtualKey, IsEnabled)
            VALUES ($id, $snippetId, $modifiers, $virtualKey, 1)
            ON CONFLICT(SnippetId) DO UPDATE SET
                Modifiers = excluded.Modifiers, VirtualKey = excluded.VirtualKey, IsEnabled = 1;
            """;
        command.Parameters.AddWithValue("$id", shortcut.Id);
        command.Parameters.AddWithValue("$snippetId", shortcut.SnippetId);
        command.Parameters.AddWithValue("$modifiers", shortcut.Modifiers);
        command.Parameters.AddWithValue("$virtualKey", shortcut.VirtualKey);

        try
        {
            await command.ExecuteNonQueryAsync(ct);
        }
        catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == SqliteConstraintUnique)
        {
            throw new DuplicateShortcutException(FormatCombo(modifiers, virtualKey));
        }

        return shortcut;
    }

    public async Task RemoveAsync(string snippetId, CancellationToken ct = default)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Shortcut WHERE SnippetId = $snippetId;";
        command.Parameters.AddWithValue("$snippetId", snippetId);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static Shortcut ReadShortcut(SqliteDataReader reader) => new()
    {
        Id = reader.GetString(0),
        SnippetId = reader.GetString(1),
        Modifiers = reader.GetInt32(2),
        VirtualKey = reader.GetInt32(3),
        IsEnabled = reader.GetInt64(4) != 0,
    };

    private static string FormatCombo(int modifiers, int virtualKey) => $"({modifiers}, {virtualKey})";
}
