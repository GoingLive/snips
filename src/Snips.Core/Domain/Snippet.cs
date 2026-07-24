namespace Snips.Core.Domain;

/// <summary>Mirrors the Snippet table in SPEC.md §4.3.</summary>
public sealed class Snippet
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public string Description { get; set; } = string.Empty;
    public string BodyHtml { get; set; } = string.Empty;
    public string PlainText { get; set; } = string.Empty;
    public bool IsRichText { get; set; } = true;
    public string? FolderId { get; set; }
    public bool IsFavorite { get; set; }

    /// <summary>User-defined drag order, meaningful only among favorites — everything else
    /// sorts alphabetically instead (Roland, 2026-07-24). Ties (e.g. 0 for every snippet that's
    /// never been reordered) fall back to Name, so a newly-favorited snippet doesn't jump to
    /// an arbitrary position.</summary>
    public int FavoriteSortOrder { get; set; }

    public int UseCount { get; set; }
    public DateTime? LastUsedUtc { get; set; }
    public required DateTime CreatedUtc { get; set; }
    public required DateTime ModifiedUtc { get; set; }
}
