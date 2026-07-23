using Snips.Core.Templates;

namespace Snips.Tests.Templates;

public class BuiltInVariableCatalogTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 7, 24, 12, 0, 0, TimeSpan.FromHours(2));

    /// <summary>Every catalog entry must actually resolve through the real engine — this is what
    /// keeps BuiltInVariableCatalog from silently drifting out of sync with
    /// BuiltInVariables.TryResolveAsync's switch statement the way the editor's insertion list
    /// already has once this session. A name that doesn't resolve renders back as the literal
    /// "{{name}}" (TemplateEngine's documented unknown-name fallback), so that's the signal.</summary>
    [Fact]
    public async Task EveryCatalogEntry_ActuallyResolves()
    {
        var context = new TemplateContext
        {
            Now = FixedNow,
            SystemInfo = new FakeSystemInfoProvider(),
            SnippetName = "Test",
            Counters = new FakeCounterStore(),
        };

        var unresolved = new List<string>();
        foreach (var entry in BuiltInVariableCatalog.All)
        {
            var result = await TemplateEngine.RenderAsync($"{{{{{entry.Name}}}}}", context);
            if (result.Text == $"{{{{{entry.Name}}}}}")
                unresolved.Add(entry.Name);
        }

        Assert.True(unresolved.Count == 0, $"Catalog entries that don't actually resolve: {string.Join(", ", unresolved)}");
    }

    [Fact]
    public void CatalogHasNoDuplicateNames()
    {
        var duplicates = BuiltInVariableCatalog.All
            .GroupBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.True(duplicates.Count == 0, $"Duplicate catalog entries: {string.Join(", ", duplicates)}");
    }
}
