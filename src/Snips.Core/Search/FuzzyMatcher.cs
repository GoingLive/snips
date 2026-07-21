namespace Snips.Core.Search;

/// <summary>
/// Scores a query as a fuzzy subsequence of a target string (e.g. "mfu" matching
/// "Meeting Follow-Up"), rewarding tight clusters and word-boundary starts.
/// </summary>
internal static class FuzzyMatcher
{
    public static (double Score, IReadOnlyList<int> Positions)? Match(string query, string target)
    {
        var queryLower = query.ToLowerInvariant();
        var targetLower = target.ToLowerInvariant();

        var leftmost = GreedyMatch(queryLower, targetLower, fromLeft: true);
        if (leftmost is null)
            return null; // not a subsequence at all

        var rightmost = GreedyMatch(queryLower, targetLower, fromLeft: false)!;

        var leftScore = Score(leftmost, target);
        var rightScore = Score(rightmost, target);

        return leftScore >= rightScore ? (leftScore, leftmost) : (rightScore, rightmost);
    }

    /// <summary>
    /// Finds one valid set of match positions by scanning greedily from one end. This is not
    /// globally optimal (a full DP alignment would be), but combined with trying both directions
    /// it reliably finds a tight cluster for the short, human-authored names snippets have.
    /// </summary>
    private static List<int>? GreedyMatch(string query, string target, bool fromLeft)
    {
        var positions = new List<int>(query.Length);

        if (fromLeft)
        {
            var searchFrom = 0;
            foreach (var c in query)
            {
                var index = target.IndexOf(c, searchFrom);
                if (index < 0)
                    return null;
                positions.Add(index);
                searchFrom = index + 1;
            }
        }
        else
        {
            var searchTo = target.Length - 1;
            for (var i = query.Length - 1; i >= 0; i--)
            {
                var index = target.LastIndexOf(query[i], searchTo);
                if (index < 0)
                    return null;
                positions.Add(index);
                searchTo = index - 1;
            }
            positions.Reverse();
        }

        return positions;
    }

    private static double Score(List<int> positions, string target)
    {
        double score = 0;

        for (var i = 0; i < positions.Count; i++)
        {
            var pos = positions[i];
            score += 10;

            if (pos == 0 || IsWordBoundary(target, pos))
                score += 15;

            if (i > 0 && positions[i - 1] == pos - 1)
                score += 10;
        }

        var span = positions[^1] - positions[0] + 1;
        var looseness = span - positions.Count;
        score -= looseness * 1.0;

        // Normalize into a bounded [0, 1000) range regardless of query/target length.
        var maxPossible = positions.Count * 35;
        return maxPossible <= 0 ? 0 : Math.Clamp(score / maxPossible * 999, 0, 999);
    }

    private static bool IsWordBoundary(string target, int index)
    {
        if (index == 0)
            return true;

        var previous = target[index - 1];
        var current = target[index];

        if (!char.IsLetterOrDigit(previous))
            return true; // preceded by space, hyphen, underscore, punctuation, etc.

        if (char.IsUpper(current) && char.IsLower(previous))
            return true; // camelCase transition

        return false;
    }
}
