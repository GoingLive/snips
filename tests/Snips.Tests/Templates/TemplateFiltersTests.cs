using Snips.Core.Templates;

namespace Snips.Tests.Templates;

public class TemplateFiltersTests
{
    [Theory]
    [InlineData("upper", "hello", "HELLO")]
    [InlineData("lower", "HELLO", "hello")]
    [InlineData("title", "hello world", "Hello World")]
    [InlineData("capitalize", "hello world", "Hello world")]
    [InlineData("trim", "  hello  ", "hello")]
    [InlineData("reverse", "abc", "cba")]
    [InlineData("nospaces", "a b\tc\nd", "abcd")]
    public void SimpleFilters_ProduceExpectedOutput(string filter, string input, string expected)
    {
        Assert.Equal(expected, TemplateFilters.Apply(filter, input, []));
    }

    [Theory]
    [InlineData("Hello, World! Foo_Bar", "hello-world-foo-bar")]
    [InlineData("  leading and trailing  ", "leading-and-trailing")]
    public void Slug_LowercasesAndDashesNonAlphanumericRuns(string input, string expected)
    {
        Assert.Equal(expected, TemplateFilters.Apply("slug", input, []));
    }

    [Fact]
    public void Base64_RoundTripsThroughUnbase64()
    {
        var encoded = TemplateFilters.Apply("base64", "Hüttmann", []);
        var decoded = TemplateFilters.Apply("unbase64", encoded, []);

        Assert.Equal("Hüttmann", decoded);
    }

    [Fact]
    public void Unbase64_InvalidInput_ReturnsInputUnchanged()
    {
        Assert.Equal("not valid base64!!", TemplateFilters.Apply("unbase64", "not valid base64!!", []));
    }

    [Fact]
    public void Md5_MatchesKnownHash()
    {
        Assert.Equal("5d41402abc4b2a76b9719d911017c592", TemplateFilters.Apply("md5", "hello", []));
    }

    [Fact]
    public void Sha256_MatchesKnownHash()
    {
        Assert.Equal(
            "2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824",
            TemplateFilters.Apply("sha256", "hello", []));
    }

    [Theory]
    [InlineData("Hello World", new[] { "5" }, "Hello")]
    [InlineData("Hi", new[] { "10" }, "Hi")] // shorter than N: unchanged
    public void Left_TakesFirstNCharacters(string input, string[] args, string expected)
    {
        Assert.Equal(expected, TemplateFilters.Apply("left", input, args));
    }

    [Theory]
    [InlineData("Hello World", new[] { "5" }, "World")]
    [InlineData("Hi", new[] { "10" }, "Hi")]
    public void Right_TakesLastNCharacters(string input, string[] args, string expected)
    {
        Assert.Equal(expected, TemplateFilters.Apply("right", input, args));
    }

    [Fact]
    public void Replace_SwapsFirstArgWithSecond()
    {
        Assert.Equal("hxllo", TemplateFilters.Apply("replace", "hello", ["e,x"]));
    }

    [Fact]
    public void PadLeft_PadsWithGivenCharacter()
    {
        Assert.Equal("0042", TemplateFilters.Apply("padleft", "42", ["4,0"]));
    }

    [Fact]
    public void PadRight_PadsWithGivenCharacter()
    {
        Assert.Equal("42--", TemplateFilters.Apply("padright", "42", ["4,-"]));
    }

    [Fact]
    public void EscapeXml_EscapesReservedCharacters()
    {
        Assert.Equal("&lt;a&gt; &amp; &quot;b&quot; &apos;c&apos;", TemplateFilters.Apply("escapexml", "<a> & \"b\" 'c'", []));
    }

    [Fact]
    public void EscapeJson_EscapesQuotesAndBackslashesWithoutAddingSurroundingQuotes()
    {
        Assert.Equal(@"line1\nline2 \""quoted\""", TemplateFilters.Apply("escapejson", "line1\nline2 \"quoted\"", []));
    }

    [Fact]
    public void UnknownFilterName_ReturnsInputUnchanged()
    {
        Assert.Equal("hello", TemplateFilters.Apply("not-a-real-filter", "hello", []));
    }

    [Fact]
    public void LeftWithNonNumericArg_ReturnsInputUnchanged()
    {
        Assert.Equal("hello", TemplateFilters.Apply("left", "hello", ["notanumber"]));
    }
}
