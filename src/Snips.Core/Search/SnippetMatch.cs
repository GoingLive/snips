using Snips.Core.Domain;

namespace Snips.Core.Search;

public sealed record SnippetMatch(Snippet Snippet, double Score, IReadOnlyList<int> NameHighlights);
