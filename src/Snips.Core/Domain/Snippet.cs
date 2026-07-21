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
    public int UseCount { get; set; }
    public DateTime? LastUsedUtc { get; set; }
    public required DateTime CreatedUtc { get; set; }
    public required DateTime ModifiedUtc { get; set; }
}
