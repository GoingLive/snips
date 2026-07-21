using Snips.Core.Domain;

namespace Snips.Core.Search;

/// <summary>
/// Ranks snippets against a query per SPEC.md §5.6: name match tiers (exact, prefix,
/// fuzzy subsequence) rank above description and body matches, and a frecency factor
/// derived from UseCount/LastUsedUtc breaks ties within a tier.
/// </summary>
public static class SnippetSearch
{
    // Large enough relative to the sub-scores below (each < 1000) and to the frecency
    // factor's max ratio (1.20, see FrecencyFactor) that frecency can only reorder
    // items within a tier, never let a lower tier outrank a higher one.
    private const double TierWeight = 1_000_000;

    /// <summary>
    /// With an empty query, returns every snippet unscored in the order given (the caller
    /// is expected to have already applied the recency ordering from ISnippetRepository.ListAsync).
    /// </summary>
    public static IReadOnlyList<SnippetMatch> Search(IEnumerable<Snippet> snippets, string query, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(query))
            return snippets.Select(s => new SnippetMatch(s, 0, [])).ToList();

        var trimmed = query.Trim();
        var matches = new List<SnippetMatch>();

        foreach (var snippet in snippets)
        {
            var match = TryMatch(snippet, trimmed);
            if (match is not null)
                matches.Add(match with { Score = match.Score * FrecencyFactor(snippet, nowUtc) });
        }

        return matches.OrderByDescending(m => m.Score).ToList();
    }

    private static SnippetMatch? TryMatch(Snippet snippet, string query)
    {
        var name = snippet.Name;

        if (string.Equals(name, query, StringComparison.OrdinalIgnoreCase))
            return new SnippetMatch(snippet, 5 * TierWeight, AllIndices(name.Length));

        if (name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            var subScore = 1000.0 * query.Length / Math.Max(name.Length, 1);
            return new SnippetMatch(snippet, 4 * TierWeight + subScore, AllIndices(query.Length));
        }

        var fuzzy = FuzzyMatcher.Match(query, name);
        if (fuzzy is not null)
            return new SnippetMatch(snippet, 3 * TierWeight + fuzzy.Value.Score, fuzzy.Value.Positions);

        var descriptionIndex = snippet.Description.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (descriptionIndex >= 0)
            return new SnippetMatch(snippet, 2 * TierWeight + 1000.0 / (1 + descriptionIndex), []);

        var bodyIndex = snippet.PlainText.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (bodyIndex >= 0)
            return new SnippetMatch(snippet, 1 * TierWeight + 1000.0 / (1 + bodyIndex), []);

        return null;
    }

    /// <summary>
    /// Bounded to [1.0, 1.20] so that even the most frecent low-tier match can never
    /// outrank the least frecent match one tier above it (the tightest adjacent-tier
    /// gap, exact vs. prefix, approaches a ratio of 5/4 = 1.25 as TierWeight dominates).
    /// </summary>
    private static double FrecencyFactor(Snippet snippet, DateTime nowUtc)
    {
        var recencyBoost = snippet.LastUsedUtc switch
        {
            null => 0.0,
            var t when (nowUtc - t.Value).TotalDays < 1 => 0.10,
            var t when (nowUtc - t.Value).TotalDays < 7 => 0.07,
            var t when (nowUtc - t.Value).TotalDays < 30 => 0.04,
            _ => 0.01,
        };

        var useCountBoost = Math.Min(0.10, Math.Log2(snippet.UseCount + 1) * 0.02);

        return 1.0 + recencyBoost + useCountBoost;
    }

    private static IReadOnlyList<int> AllIndices(int count) => Enumerable.Range(0, count).ToList();
}
