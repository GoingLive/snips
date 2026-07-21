using System.Globalization;
using Microsoft.Data.Sqlite;
using Snips.Core.Domain;
using Snips.Core.Id;
using Snips.Core.Repositories;

namespace Snips.Data.Repositories;

public sealed class SqliteSnippetRepository(SqliteConnection connection, SnowflakeIdGenerator idGenerator)
    : ISnippetRepository
{
    private const int SqliteConstraintUnique = 2067; // SQLITE_CONSTRAINT_UNIQUE

    private const string Columns =
        "Id, Name, Description, BodyHtml, PlainText, IsRichText, FolderId, IsFavorite, UseCount, LastUsedUtc, CreatedUtc, ModifiedUtc";

    public async Task<Snippet> CreateAsync(Snippet snippet, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var toInsert = new Snippet
        {
            Id = idGenerator.NextId(),
            Name = snippet.Name,
            Description = snippet.Description,
            BodyHtml = snippet.BodyHtml,
            PlainText = snippet.PlainText,
            IsRichText = snippet.IsRichText,
            FolderId = snippet.FolderId,
            IsFavorite = snippet.IsFavorite,
            UseCount = snippet.UseCount,
            LastUsedUtc = snippet.LastUsedUtc,
            CreatedUtc = now,
            ModifiedUtc = now,
        };

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            INSERT INTO Snippet ({Columns})
            VALUES ($id, $name, $description, $bodyHtml, $plainText, $isRichText, $folderId,
                    $isFavorite, $useCount, $lastUsedUtc, $createdUtc, $modifiedUtc);
            """;
        BindSnippetParameters(command, toInsert);

        try
        {
            await command.ExecuteNonQueryAsync(ct);
        }
        catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == SqliteConstraintUnique)
        {
            throw new DuplicateSnippetNameException(snippet.Name);
        }

        return toInsert;
    }

    public async Task<Snippet?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {Columns} FROM Snippet WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);

        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadSnippet(reader) : null;
    }

    public async Task<Snippet?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {Columns} FROM Snippet WHERE Name = $name COLLATE NOCASE;";
        command.Parameters.AddWithValue("$name", name);

        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadSnippet(reader) : null;
    }

    public async Task<IReadOnlyList<Snippet>> ListAsync(CancellationToken ct = default)
    {
        await using var command = connection.CreateCommand();
        // NULLs sort last in a DESC ordering in SQLite, so never-used snippets fall
        // after recently-used ones without a CASE expression. See SPEC.md §5.6.
        command.CommandText = $"SELECT {Columns} FROM Snippet ORDER BY LastUsedUtc DESC, Name COLLATE NOCASE ASC;";

        var results = new List<Snippet>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            results.Add(ReadSnippet(reader));

        return results;
    }

    public async Task UpdateAsync(Snippet snippet, CancellationToken ct = default)
    {
        var toUpdate = new Snippet
        {
            Id = snippet.Id,
            Name = snippet.Name,
            Description = snippet.Description,
            BodyHtml = snippet.BodyHtml,
            PlainText = snippet.PlainText,
            IsRichText = snippet.IsRichText,
            FolderId = snippet.FolderId,
            IsFavorite = snippet.IsFavorite,
            UseCount = snippet.UseCount,
            LastUsedUtc = snippet.LastUsedUtc,
            CreatedUtc = snippet.CreatedUtc,
            ModifiedUtc = DateTime.UtcNow,
        };

        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Snippet SET
                Name = $name, Description = $description, BodyHtml = $bodyHtml, PlainText = $plainText,
                IsRichText = $isRichText, FolderId = $folderId, IsFavorite = $isFavorite,
                UseCount = $useCount, LastUsedUtc = $lastUsedUtc, ModifiedUtc = $modifiedUtc
            WHERE Id = $id;
            """;
        BindSnippetParameters(command, toUpdate);

        int rowsAffected;
        try
        {
            rowsAffected = await command.ExecuteNonQueryAsync(ct);
        }
        catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == SqliteConstraintUnique)
        {
            throw new DuplicateSnippetNameException(snippet.Name);
        }

        if (rowsAffected == 0)
            throw new KeyNotFoundException($"No snippet with id '{snippet.Id}'.");

        snippet.ModifiedUtc = toUpdate.ModifiedUtc;
    }

    public async Task RecordUseAsync(string id, CancellationToken ct = default)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Snippet SET UseCount = UseCount + 1, LastUsedUtc = $now WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$now", ToIso(DateTime.UtcNow));
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken ct = default)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Snippet WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);
        var rowsAffected = await command.ExecuteNonQueryAsync(ct);
        return rowsAffected > 0;
    }

    private static void BindSnippetParameters(SqliteCommand command, Snippet s)
    {
        command.Parameters.AddWithValue("$id", s.Id);
        command.Parameters.AddWithValue("$name", s.Name);
        command.Parameters.AddWithValue("$description", s.Description);
        command.Parameters.AddWithValue("$bodyHtml", s.BodyHtml);
        command.Parameters.AddWithValue("$plainText", s.PlainText);
        command.Parameters.AddWithValue("$isRichText", s.IsRichText ? 1 : 0);
        command.Parameters.AddWithValue("$folderId", (object?)s.FolderId ?? DBNull.Value);
        command.Parameters.AddWithValue("$isFavorite", s.IsFavorite ? 1 : 0);
        command.Parameters.AddWithValue("$useCount", s.UseCount);
        command.Parameters.AddWithValue("$lastUsedUtc", (object?)ToIsoOrNull(s.LastUsedUtc) ?? DBNull.Value);
        command.Parameters.AddWithValue("$createdUtc", ToIso(s.CreatedUtc));
        command.Parameters.AddWithValue("$modifiedUtc", ToIso(s.ModifiedUtc));
    }

    private static Snippet ReadSnippet(SqliteDataReader reader) => new()
    {
        Id = reader.GetString(0),
        Name = reader.GetString(1),
        Description = reader.GetString(2),
        BodyHtml = reader.GetString(3),
        PlainText = reader.GetString(4),
        IsRichText = reader.GetInt64(5) != 0,
        FolderId = reader.IsDBNull(6) ? null : reader.GetString(6),
        IsFavorite = reader.GetInt64(7) != 0,
        UseCount = (int)reader.GetInt64(8),
        LastUsedUtc = reader.IsDBNull(9) ? null : FromIso(reader.GetString(9)),
        CreatedUtc = FromIso(reader.GetString(10)),
        ModifiedUtc = FromIso(reader.GetString(11)),
    };

    private static string ToIso(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture);

    private static string? ToIsoOrNull(DateTime? value) => value is null ? null : ToIso(value.Value);

    private static DateTime FromIso(string value) =>
        DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
