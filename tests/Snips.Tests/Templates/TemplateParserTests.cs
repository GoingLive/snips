using Snips.Core.Templates;

namespace Snips.Tests.Templates;

public class TemplateParserTests
{
    [Fact]
    public void PlainText_WithNoPlaceholders_IsOneLiteralSegment()
    {
        var segments = TemplateParser.Parse("Dear customer, thank you.");

        var literal = Assert.Single(segments);
        Assert.Equal(new LiteralSegment("Dear customer, thank you."), literal);
    }

    [Fact]
    public void SimplePlaceholder_HasNoArgsOrFilters()
    {
        var segments = TemplateParser.Parse("{{date}}");

        var placeholder = Assert.IsType<PlaceholderSegment>(Assert.Single(segments));
        Assert.Equal("date", placeholder.Name);
        Assert.Empty(placeholder.Args);
        Assert.Empty(placeholder.Filters);
    }

    [Fact]
    public void PlaceholderWithColonArgs_SplitsPositionally()
    {
        var segments = TemplateParser.Parse("{{date:+7d:dd.MM.yyyy}}");

        var placeholder = Assert.IsType<PlaceholderSegment>(Assert.Single(segments));
        Assert.Equal("date", placeholder.Name);
        Assert.Equal(["+7d", "dd.MM.yyyy"], placeholder.Args);
    }

    [Fact]
    public void PlaceholderWithFilterChain_ParsesEachFilterAndItsArgs()
    {
        var segments = TemplateParser.Parse("{{clipboard|trim|left:5}}");

        var placeholder = Assert.IsType<PlaceholderSegment>(Assert.Single(segments));
        Assert.Equal("clipboard", placeholder.Name);
        Assert.Equal(2, placeholder.Filters.Count);
        Assert.Equal("trim", placeholder.Filters[0].Name);
        Assert.Empty(placeholder.Filters[0].Args);
        Assert.Equal("left", placeholder.Filters[1].Name);
        Assert.Equal(["5"], placeholder.Filters[1].Args);
    }

    [Fact]
    public void InteractivePlaceholder_DefaultValueUsesColon_NotPipe()
    {
        // Grammar-fix from SPEC.md §7.6: colon for args, pipe reserved for filters,
        // so a default value and a filter can coexist unambiguously.
        var segments = TemplateParser.Parse("{{input:Name:Roland|upper}}");

        var placeholder = Assert.IsType<PlaceholderSegment>(Assert.Single(segments));
        Assert.Equal("input", placeholder.Name);
        Assert.Equal(["Name", "Roland"], placeholder.Args);
        var filter = Assert.Single(placeholder.Filters);
        Assert.Equal("upper", filter.Name);
        Assert.Empty(filter.Args);
    }

    [Fact]
    public void MixOfLiteralAndPlaceholderSegments_PreservesOrder()
    {
        var segments = TemplateParser.Parse("Dear {{input:Name}}, today is {{date}}.");

        Assert.Equal(5, segments.Count);
        Assert.Equal(new LiteralSegment("Dear "), segments[0]);
        Assert.Equal("input", ((PlaceholderSegment)segments[1]).Name);
        Assert.Equal(new LiteralSegment(", today is "), segments[2]);
        Assert.Equal("date", ((PlaceholderSegment)segments[3]).Name);
        Assert.Equal(new LiteralSegment("."), segments[4]);
    }

    [Fact]
    public void EscapedDoubleBrace_ProducesLiteralTextNotAPlaceholder()
    {
        var segments = TemplateParser.Parse(@"Use \{{like this}} literally.");

        var literal = Assert.IsType<LiteralSegment>(Assert.Single(segments));
        Assert.Equal("Use {{like this}} literally.", literal.Text);
    }

    [Fact]
    public void UnterminatedPlaceholder_IsTreatedAsLiteralText_NotAnError()
    {
        var segments = TemplateParser.Parse("Hello {{date world");

        var literal = Assert.IsType<LiteralSegment>(Assert.Single(segments));
        Assert.Equal("Hello {{date world", literal.Text);
    }

    [Fact]
    public void EmptyString_ProducesNoSegments()
    {
        Assert.Empty(TemplateParser.Parse(""));
    }

    [Fact]
    public void FilterWithMultipleArgs_ParsesAllOfThem()
    {
        var segments = TemplateParser.Parse("{{name|replace:a,b}}");

        var placeholder = Assert.IsType<PlaceholderSegment>(Assert.Single(segments));
        var filter = Assert.Single(placeholder.Filters);
        Assert.Equal("replace", filter.Name);
        Assert.Equal(["a,b"], filter.Args);
    }
}
