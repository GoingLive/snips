namespace Snips.Core.Domain;

/// <summary>Mirrors the Language table — see docs/language-pack-brief.md. English is not a row
/// here: it IS the master key set every BuiltInVariableCatalog name is written in, nothing to
/// translate it to.</summary>
public sealed class Language
{
    public required string Code { get; set; }
    public required string DisplayName { get; set; }
    public bool IsRightToLeft { get; set; }
}
