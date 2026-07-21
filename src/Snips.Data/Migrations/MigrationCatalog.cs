namespace Snips.Data.Migrations;

/// <summary>The ordered set of schema migrations. See SPEC.md §4.3 for the canonical schema.</summary>
internal static class MigrationCatalog
{
    public static readonly IReadOnlyList<Migration> All =
    [
        new Migration(1, "InitialSchema", """
            CREATE TABLE Snippet (
                Id            TEXT    PRIMARY KEY,
                Name          TEXT    NOT NULL,
                Description   TEXT    NOT NULL DEFAULT '',
                BodyHtml      TEXT    NOT NULL DEFAULT '',
                PlainText     TEXT    NOT NULL DEFAULT '',
                IsRichText    INTEGER NOT NULL DEFAULT 1,
                FolderId      TEXT    NULL REFERENCES Folder(Id) ON DELETE SET NULL,
                IsFavorite    INTEGER NOT NULL DEFAULT 0,
                UseCount      INTEGER NOT NULL DEFAULT 0,
                LastUsedUtc   TEXT    NULL,
                CreatedUtc    TEXT    NOT NULL,
                ModifiedUtc   TEXT    NOT NULL
            );
            CREATE UNIQUE INDEX IX_Snippet_Name ON Snippet(Name COLLATE NOCASE);
            CREATE INDEX IX_Snippet_LastUsed ON Snippet(LastUsedUtc DESC);

            CREATE TABLE SnippetAsset (
                Id          TEXT PRIMARY KEY,
                SnippetId   TEXT NOT NULL REFERENCES Snippet(Id) ON DELETE CASCADE,
                ContentHash TEXT NOT NULL,
                MimeType    TEXT NOT NULL,
                Bytes       BLOB NOT NULL,
                Width       INTEGER NOT NULL,
                Height      INTEGER NOT NULL
            );
            CREATE INDEX IX_Asset_Snippet ON SnippetAsset(SnippetId);

            CREATE TABLE Shortcut (
                Id          TEXT    PRIMARY KEY,
                SnippetId   TEXT    NOT NULL UNIQUE REFERENCES Snippet(Id) ON DELETE CASCADE,
                Modifiers   INTEGER NOT NULL,
                VirtualKey  INTEGER NOT NULL,
                IsEnabled   INTEGER NOT NULL DEFAULT 1
            );
            CREATE UNIQUE INDEX IX_Shortcut_Combo ON Shortcut(Modifiers, VirtualKey);

            CREATE TABLE Variable (
                Id          TEXT PRIMARY KEY,
                Name        TEXT NOT NULL,
                Description TEXT NOT NULL DEFAULT '',
                Kind        TEXT NOT NULL,
                Language    TEXT NOT NULL DEFAULT 'none',
                Body        TEXT NOT NULL,
                CacheScope  TEXT NOT NULL DEFAULT 'none',
                CreatedUtc  TEXT NOT NULL,
                ModifiedUtc TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IX_Variable_Name ON Variable(Name COLLATE NOCASE);

            CREATE TABLE Folder (
                Id        TEXT PRIMARY KEY,
                Name      TEXT NOT NULL,
                ParentId  TEXT NULL REFERENCES Folder(Id) ON DELETE CASCADE,
                SortOrder INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE Tag ( Id TEXT PRIMARY KEY, Name TEXT NOT NULL UNIQUE COLLATE NOCASE );
            CREATE TABLE SnippetTag (
                SnippetId TEXT NOT NULL REFERENCES Snippet(Id) ON DELETE CASCADE,
                TagId     TEXT NOT NULL REFERENCES Tag(Id)     ON DELETE CASCADE,
                PRIMARY KEY (SnippetId, TagId)
            );

            CREATE TABLE Counter ( Name TEXT PRIMARY KEY COLLATE NOCASE, Value INTEGER NOT NULL DEFAULT 0 );
            CREATE TABLE Setting ( Key TEXT PRIMARY KEY, Value TEXT NOT NULL );
            """),
    ];
}
