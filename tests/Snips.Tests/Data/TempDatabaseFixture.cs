using System.IO;
using Microsoft.Data.Sqlite;
using Snips.Data;

namespace Snips.Tests.Data;

/// <summary>Opens a SnipsDatabase against a fresh temp file and deletes it (plus -wal/-shm) afterwards.</summary>
public sealed class TempDatabaseFixture : IAsyncLifetime
{
    public string DatabasePath { get; } = Path.Combine(Path.GetTempPath(), $"snips-test-{Guid.NewGuid():N}.db");

    public SnipsDatabase Database { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Database = await SnipsDatabase.OpenAsync(DatabasePath);
    }

    public async Task DisposeAsync()
    {
        await Database.DisposeAsync();

        // Microsoft.Data.Sqlite pools native sqlite3 handles by default; disposing the
        // SqliteConnection returns it to the pool rather than releasing the file lock.
        // Without this, deleting the temp file below fails with "file in use".
        SqliteConnection.ClearAllPools();

        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            var file = DatabasePath + suffix;
            if (File.Exists(file))
                File.Delete(file);
        }

        var directory = Path.GetTempPath();
        var prefix = Path.GetFileName(DatabasePath) + ".backup-";
        foreach (var backup in Directory.GetFiles(directory).Where(f => Path.GetFileName(f).StartsWith(prefix)))
            File.Delete(backup);
    }
}
