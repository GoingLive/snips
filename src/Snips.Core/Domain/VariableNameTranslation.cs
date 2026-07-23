namespace Snips.Core.Domain;

/// <summary>Mirrors the VariableNameTranslation table. Maps a user-facing LocalName (what
/// someone types, e.g. "heute") to the MasterKey it actually resolves as (e.g. "date") for one
/// LanguageCode. See docs/language-pack-brief.md.</summary>
public sealed class VariableNameTranslation
{
    public required string Id { get; set; }
    public required string MasterKey { get; set; }
    public required string LanguageCode { get; set; }
    public required string LocalName { get; set; }
}
