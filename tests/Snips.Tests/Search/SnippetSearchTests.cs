using Snips.Core.Domain;
using Snips.Core.Search;

namespace Snips.Tests.Search;

public class SnippetSearchTests
{
    private static readonly DateTime Now = new(2026, 7, 21, 12, 0, 0, DateTimeKind.Utc);

    private static Snippet Make(
        string name,
        string description = "",
        string plainText = "",
        int useCount = 0,
        DateTime? lastUsedUtc = null) => new()
    {
        Id = Guid.NewGuid().ToString("N")[..19].PadRight(19, '0'),
        Name = name,
        Description = description,
        PlainText = plainText,
        UseCount = useCount,
        LastUsedUtc = lastUsedUtc,
        CreatedUtc = Now,
        ModifiedUtc = Now,
    };

    [Fact]
    public void EmptyQuery_ReturnsAllSnippetsInGivenOrder_Unscored()
    {
        var snippets = new[] { Make("Zebra"), Make("Alpha") };

        var results = SnippetSearch.Search(snippets, "", Now);

        Assert.Equal(["Zebra", "Alpha"], results.Select(r => r.Snippet.Name));
        Assert.All(results, r => Assert.Equal(0, r.Score));
    }

    [Fact]
    public void ExactNameMatch_AlwaysRanksAboveEverythingElse_RegardlessOfFrecency()
    {
        var exactButNeverUsed = Make("Signature");
        var fuzzyButHeavilyUsed = Make(
            "A Signature-like Snippet For Testing",
            useCount: 500,
            lastUsedUtc: Now.AddMinutes(-1));

        var results = SnippetSearch.Search([fuzzyButHeavilyUsed, exactButNeverUsed], "Signature", Now);

        Assert.Equal("Signature", results[0].Snippet.Name);
    }

    [Fact]
    public void PrefixMatch_RanksAboveFuzzySubsequenceMatch()
    {
        var prefix = Make("Invoice header");
        var fuzzy = Make("I Never Verify Old Invoices Carefully Enough");

        var results = SnippetSearch.Search([fuzzy, prefix], "Inv", Now);

        Assert.Equal("Invoice header", results[0].Snippet.Name);
    }

    [Fact]
    public void FuzzySubsequenceMatch_RanksAboveDescriptionMatch()
    {
        var fuzzy = Make("Meeting Follow-Up");
        var descriptionOnly = Make("Zzz", description: "used after a meeting follow-up call");

        var results = SnippetSearch.Search([descriptionOnly, fuzzy], "mfu", Now);

        Assert.Equal("Meeting Follow-Up", results[0].Snippet.Name);
    }

    [Fact]
    public void DescriptionMatch_RanksAboveBodyMatch()
    {
        var descriptionMatch = Make("Zzz", description: "quarterly report");
        var bodyMatch = Make("Yyy", plainText: "please find the quarterly report attached");

        var results = SnippetSearch.Search([bodyMatch, descriptionMatch], "quarterly", Now);

        Assert.Equal("Zzz", results[0].Snippet.Name);
    }

    [Fact]
    public void NonMatchingSnippets_AreExcludedFromResults()
    {
        var results = SnippetSearch.Search([Make("Completely unrelated")], "xyz123", Now);

        Assert.Empty(results);
    }

    [Fact]
    public void WithinTheSameTier_MoreRecentlyUsedRanksFirst()
    {
        var usedToday = Make("Alpha template", lastUsedUtc: Now.AddHours(-1));
        var usedLastMonth = Make("Alpha other", lastUsedUtc: Now.AddDays(-60));

        var results = SnippetSearch.Search([usedLastMonth, usedToday], "Alpha", Now);

        Assert.Equal("Alpha template", results[0].Snippet.Name);
    }

    [Fact]
    public void WordBoundaryInitials_ScoreHigherThanMidWordScatteredLetters()
    {
        var boundaryMatch = Make("Bug Report Template"); // b, r, t all at word starts
        var scatteredMatch = Make("A Better Rough Try at something"); // b,r,t exist but mid-word

        var results = SnippetSearch.Search([scatteredMatch, boundaryMatch], "brt", Now);

        Assert.Equal("Bug Report Template", results[0].Snippet.Name);
    }
}
