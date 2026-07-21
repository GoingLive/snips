namespace Snips.Data.Migrations;

internal sealed record Migration(int Version, string Name, string Sql);
