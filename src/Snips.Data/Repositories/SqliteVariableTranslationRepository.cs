using Microsoft.Data.Sqlite;
using Snips.Core.Domain;
using Snips.Core.Id;
using Snips.Core.Repositories;

namespace Snips.Data.Repositories;

public sealed class SqliteVariableTranslationRepository(SqliteConnection connection, SnowflakeIdGenerator idGenerator)
    : IVariableTranslationRepository
{
    private const int SqliteConstraintUnique = 2067; // SQLITE_CONSTRAINT_UNIQUE

    public async Task<IReadOnlyList<Language>> ListLanguagesAsync(CancellationToken ct = default)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Code, DisplayName, IsRightToLeft FROM Language ORDER BY DisplayName;";

        var results = new List<Language>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new Language
            {
                Code = reader.GetString(0),
                DisplayName = reader.GetString(1),
                IsRightToLeft = reader.GetInt64(2) != 0,
            });
        }

        return results;
    }

    public async Task<Language> AddOrUpdateLanguageAsync(
        string code, string displayName, bool isRightToLeft, CancellationToken ct = default)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Language (Code, DisplayName, IsRightToLeft)
            VALUES ($code, $displayName, $isRtl)
            ON CONFLICT(Code) DO UPDATE SET DisplayName = excluded.DisplayName, IsRightToLeft = excluded.IsRightToLeft;
            """;
        command.Parameters.AddWithValue("$code", code);
        command.Parameters.AddWithValue("$displayName", displayName);
        command.Parameters.AddWithValue("$isRtl", isRightToLeft ? 1 : 0);
        await command.ExecuteNonQueryAsync(ct);

        return new Language { Code = code, DisplayName = displayName, IsRightToLeft = isRightToLeft };
    }

    public async Task<IReadOnlyList<VariableNameTranslation>> ListTranslationsAsync(
        string languageCode, CancellationToken ct = default)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, MasterKey, LanguageCode, LocalName FROM VariableNameTranslation WHERE LanguageCode = $languageCode;";
        command.Parameters.AddWithValue("$languageCode", languageCode);

        var results = new List<VariableNameTranslation>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new VariableNameTranslation
            {
                Id = reader.GetString(0),
                MasterKey = reader.GetString(1),
                LanguageCode = reader.GetString(2),
                LocalName = reader.GetString(3),
            });
        }

        return results;
    }

    /// <summary>The schema's unique index only stops two masterKeys sharing one LocalName within
    /// a language — it doesn't stop one masterKey from having several LocalName rows. Rather than
    /// build a UI that has to handle "which of N local names is THE one," this delete-then-insert
    /// keeps a de facto one-local-name-per-masterKey-per-language relationship at the application
    /// level, leaving the schema free to relax that later if a real need for synonyms shows up.</summary>
    public async Task<VariableNameTranslation> SetAsync(
        string masterKey, string languageCode, string localName, CancellationToken ct = default)
    {
        var translation = new VariableNameTranslation
        {
            Id = idGenerator.NextId(),
            MasterKey = masterKey,
            LanguageCode = languageCode,
            LocalName = localName,
        };

        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(ct);

        await using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM VariableNameTranslation WHERE LanguageCode = $languageCode AND MasterKey = $masterKey;";
            delete.Parameters.AddWithValue("$languageCode", languageCode);
            delete.Parameters.AddWithValue("$masterKey", masterKey);
            await delete.ExecuteNonQueryAsync(ct);
        }

        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO VariableNameTranslation (Id, MasterKey, LanguageCode, LocalName)
                VALUES ($id, $masterKey, $languageCode, $localName);
                """;
            insert.Parameters.AddWithValue("$id", translation.Id);
            insert.Parameters.AddWithValue("$masterKey", translation.MasterKey);
            insert.Parameters.AddWithValue("$languageCode", translation.LanguageCode);
            insert.Parameters.AddWithValue("$localName", translation.LocalName);

            try
            {
                await insert.ExecuteNonQueryAsync(ct);
            }
            catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == SqliteConstraintUnique)
            {
                throw new DuplicateVariableTranslationException(localName, languageCode);
            }
        }

        await transaction.CommitAsync(ct);
        return translation;
    }

    public async Task RemoveAsync(string masterKey, string languageCode, CancellationToken ct = default)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM VariableNameTranslation WHERE LanguageCode = $languageCode AND MasterKey = $masterKey;";
        command.Parameters.AddWithValue("$languageCode", languageCode);
        command.Parameters.AddWithValue("$masterKey", masterKey);
        await command.ExecuteNonQueryAsync(ct);
    }
}
