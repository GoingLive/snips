using Snips.Core.Templates;

namespace Snips.Tests.Templates;

public class TemplateEngineTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 7, 21, 11, 46, 3, TimeSpan.FromHours(2));

    private static TemplateContext MakeContext(
        string snippetName = "Test Snippet", string snippetDescription = "", int useCount = 0,
        string? clipboard = null, string? activeWindow = null, string? activeApp = null,
        ICounterStore? counters = null, IInteractivePrompt? prompt = null,
        IReadOnlyDictionary<string, string>? externalVariables = null) => new()
    {
        Now = FixedNow,
        SystemInfo = new FakeSystemInfoProvider(),
        SnippetName = snippetName,
        SnippetDescription = snippetDescription,
        UseCount = useCount,
        ClipboardText = clipboard,
        ActiveWindowTitle = activeWindow,
        ActiveAppName = activeApp,
        Counters = counters,
        Prompt = prompt,
        ExternalVariables = externalVariables,
    };

    // --- §7.1 Date and time -------------------------------------------------------------

    [Theory]
    [InlineData("{{year}}", "2026")]
    [InlineData("{{year2}}", "26")]
    [InlineData("{{month}}", "07")]
    [InlineData("{{day}}", "21")]
    [InlineData("{{hour}}", "11")]
    [InlineData("{{minute}}", "46")]
    [InlineData("{{second}}", "03")]
    [InlineData("{{date}}", "2026-07-21")]
    [InlineData("{{time}}", "11:46:03")]
    [InlineData("{{datetime}}", "2026-07-21 11:46:03")]
    [InlineData("{{weekday}}", "Tuesday")]
    [InlineData("{{monthname}}", "July")]
    [InlineData("{{quarter}}", "Q3")]
    [InlineData("{{daysinmonth}}", "31")]
    [InlineData("{{utcoffset}}", "+02:00")]
    public async Task FixedFormatDateVariables_MatchExpectedOutput(string template, string expected)
    {
        var result = await TemplateEngine.RenderAsync(template, MakeContext());
        Assert.Equal(expected, result.Text);
    }

    [Fact]
    public async Task Date_WithOffsetAndFormat_AppliesBoth()
    {
        var result = await TemplateEngine.RenderAsync("{{date:+7d:dd.MM.yyyy}}", MakeContext());
        Assert.Equal("28.07.2026", result.Text);
    }

    [Fact]
    public async Task Date_WithFormatOnlyArg_IsNotMisreadAsAnOffset()
    {
        // Regression: a lone format arg used to be assumed to be the offset (position 0),
        // silently fail to match the offset grammar, and fall back to the untouched default
        // format — so {{date:dd.MM.yyyy}} rendered as "2026-07-21" instead of "21.07.2026".
        var result = await TemplateEngine.RenderAsync("{{date:dd.MM.yyyy}}", MakeContext());
        Assert.Equal("21.07.2026", result.Text);
    }

    [Fact]
    public async Task Date_WithOffsetOnlyArg_StillAppliesTheOffset()
    {
        var result = await TemplateEngine.RenderAsync("{{date:+7d}}", MakeContext());
        Assert.Equal("2026-07-28", result.Text);
    }

    [Fact]
    public async Task LocalDateAndTime_UseTheContextCulturesPatterns()
    {
        // MakeContext doesn't set Culture, so TemplateContext's default (InvariantCulture) applies.
        var result = await TemplateEngine.RenderAsync(
            "{{localdate}}|{{localtime}}|{{locallongdate}}|{{locallongtime}}", MakeContext());
        Assert.Equal("07/21/2026|11:46|Tuesday, 21 July 2026|11:46:03", result.Text);
    }

    [Fact]
    public async Task IntlDate_IsSpelledOutInEnglishRegardlessOfContextCulture()
    {
        var result = await TemplateEngine.RenderAsync("{{intldate}}", MakeContext());
        Assert.Equal("21 July 2026", result.Text);
    }

    [Fact]
    public async Task Tomorrow_IsOneDayAhead()
    {
        var result = await TemplateEngine.RenderAsync("{{tomorrow}}", MakeContext());
        Assert.Equal("2026-07-22", result.Text);
    }

    [Fact]
    public async Task Yesterday_WithFormat_IsOneDayBehindInGivenFormat()
    {
        var result = await TemplateEngine.RenderAsync("{{yesterday:dd.MM.yyyy}}", MakeContext());
        Assert.Equal("20.07.2026", result.Text);
    }

    [Fact]
    public async Task Now_WithFormatArg_UsesThatFormat()
    {
        var result = await TemplateEngine.RenderAsync("{{now:yyyy}}", MakeContext());
        Assert.Equal("2026", result.Text);
    }

    [Fact]
    public async Task Timestamp_MatchesUnixSecondsOfFixedNow()
    {
        var result = await TemplateEngine.RenderAsync("{{timestamp}}", MakeContext());
        Assert.Equal(FixedNow.ToUnixTimeSeconds().ToString(), result.Text);
    }

    // --- §7.2 Identity/system -------------------------------------------------------------

    [Fact]
    public async Task IdentityVariables_ComeFromTheInjectedSystemInfoProvider()
    {
        var result = await TemplateEngine.RenderAsync("{{user}} @ {{machine}} ({{os}})", MakeContext());
        Assert.Equal("roland @ DESKTOP-TEST (Windows 11 Pro)", result.Text);
    }

    // --- §7.3 Context -----------------------------------------------------------------------

    [Fact]
    public async Task Clipboard_ReflectsInjectedClipboardText()
    {
        var result = await TemplateEngine.RenderAsync("{{clipboard}}", MakeContext(clipboard: "copied text"));
        Assert.Equal("copied text", result.Text);
    }

    [Fact]
    public async Task Selection_ResolvesToEmpty_NotYetImplemented()
    {
        var result = await TemplateEngine.RenderAsync("[{{selection}}]", MakeContext());
        Assert.Equal("[]", result.Text);
    }

    // --- §7.4 Snippet metadata --------------------------------------------------------------

    [Fact]
    public async Task SnippetMetadata_ReflectsContext()
    {
        var result = await TemplateEngine.RenderAsync(
            "{{snippetname}} used {{usecount}} times", MakeContext(snippetName: "Signature", useCount: 5));
        Assert.Equal("Signature used 5 times", result.Text);
    }

    // --- §7.5 Generators --------------------------------------------------------------------

    [Fact]
    public async Task Guid_ProducesAParsableGuid()
    {
        var result = await TemplateEngine.RenderAsync("{{guid}}", MakeContext());
        Assert.True(Guid.TryParse(result.Text, out _));
    }

    [Fact]
    public async Task RandomString_ProducesRequestedLength()
    {
        var result = await TemplateEngine.RenderAsync("{{randomstring:12}}", MakeContext());
        Assert.Equal(12, result.Text.Length);
    }

    [Fact]
    public async Task Random_StaysWithinGivenRange()
    {
        var result = await TemplateEngine.RenderAsync("{{random:1-1}}", MakeContext());
        Assert.Equal("1", result.Text);
    }

    [Fact]
    public async Task Counter_IncrementsAndFormats()
    {
        var counters = new FakeCounterStore();
        var context = MakeContext(counters: counters);

        var first = await TemplateEngine.RenderAsync("{{counter:Invoice:1:0000}}", context);
        var second = await TemplateEngine.RenderAsync("{{counter:Invoice:1:0000}}", context);

        Assert.Equal("0001", first.Text);
        Assert.Equal("0002", second.Text);
    }

    // --- Filters composed with variables ------------------------------------------------

    [Fact]
    public async Task FiltersChain_AppliesInOrderAfterVariableResolution()
    {
        var result = await TemplateEngine.RenderAsync(
            "{{clipboard|trim|upper}}", MakeContext(clipboard: "  hello  "));
        Assert.Equal("HELLO", result.Text);
    }

    // --- Unknown / malformed --------------------------------------------------------------

    [Fact]
    public async Task UnknownVariableName_IsLeftVisibleRatherThanSilentlyDropped()
    {
        var result = await TemplateEngine.RenderAsync("Dear {{totallyMadeUp}},", MakeContext());
        Assert.Equal("Dear {{totallyMadeUp}},", result.Text);
    }

    // --- External variables (docs/variables.yaml "not_yet_implemented", proposal 2026-07-22) ---

    [Fact]
    public async Task ExternalVariable_ResolvesWhenNotABuiltIn()
    {
        var external = new Dictionary<string, string> { ["companyname"] = "Acme AG" };
        var result = await TemplateEngine.RenderAsync(
            "{{companyname}}", MakeContext(externalVariables: external));

        Assert.Equal("Acme AG", result.Text);
    }

    [Fact]
    public async Task ExternalVariable_LookupIsCaseInsensitive()
    {
        var external = new Dictionary<string, string> { ["CompanyName"] = "Acme AG" };
        var result = await TemplateEngine.RenderAsync(
            "{{companyname}}", MakeContext(externalVariables: external));

        Assert.Equal("Acme AG", result.Text);
    }

    [Fact]
    public async Task BuiltInVariable_TakesPrecedenceOverAnExternalVariableOfTheSameName()
    {
        // A built-in name can't be shadowed — avoids a confusing collision where an external
        // file silently changes what {{date}} means.
        var external = new Dictionary<string, string> { ["date"] = "not-a-real-date" };
        var result = await TemplateEngine.RenderAsync(
            "{{date}}", MakeContext(externalVariables: external));

        Assert.Equal("2026-07-21", result.Text);
    }

    [Fact]
    public async Task NoExternalVariablesConfigured_UnknownNameStillFallsBackToLiteral()
    {
        var result = await TemplateEngine.RenderAsync("{{companyname}}", MakeContext(externalVariables: null));

        Assert.Equal("{{companyname}}", result.Text);
    }

    [Fact]
    public async Task LiteralTextWithNoPlaceholders_PassesThroughUnchanged()
    {
        var result = await TemplateEngine.RenderAsync("Just plain text.", MakeContext());
        Assert.Equal("Just plain text.", result.Text);
        Assert.False(result.Cancelled);
    }

    // --- §7.6 Interactive prompts ----------------------------------------------------------

    [Fact]
    public async Task InteractivePlaceholders_AreCollectedIntoOneFormRequest_DedupedByLabel()
    {
        var prompt = new FakeInteractivePrompt
        {
            AnswersToReturn = new Dictionary<string, string> { ["Name"] = "Roland" },
        };
        var context = MakeContext(prompt: prompt);

        var result = await TemplateEngine.RenderAsync("Dear {{input:Name}}, hi {{input:Name}}!", context);

        Assert.Equal("Dear Roland, hi Roland!", result.Text);
        Assert.Single(prompt.LastFieldsShown!); // repeated label -> one field, asked once
    }

    [Fact]
    public async Task InteractiveDefault_UsesColonNotPipe_AndFiltersStillApply()
    {
        var prompt = new FakeInteractivePrompt
        {
            AnswersToReturn = new Dictionary<string, string> { ["Name"] = "roland" },
        };
        var context = MakeContext(prompt: prompt);

        var result = await TemplateEngine.RenderAsync("{{input:Name:Anonymous|upper}}", context);

        Assert.Equal("ROLAND", result.Text);
        var field = Assert.IsType<TextPromptField>(Assert.Single(prompt.LastFieldsShown!));
        Assert.Equal("Anonymous", field.Default);
    }

    [Fact]
    public async Task Choice_ParsesCommaSeparatedOptions()
    {
        var prompt = new FakeInteractivePrompt
        {
            AnswersToReturn = new Dictionary<string, string> { ["Size"] = "Large" },
        };
        var context = MakeContext(prompt: prompt);

        await TemplateEngine.RenderAsync("{{choice:Size:Small,Medium,Large}}", context);

        var field = Assert.IsType<ChoicePromptField>(Assert.Single(prompt.LastFieldsShown!));
        Assert.Equal(["Small", "Medium", "Large"], field.Options);
    }

    [Fact]
    public async Task Check_ProvidesCheckedAndUncheckedValues()
    {
        var prompt = new FakeInteractivePrompt
        {
            AnswersToReturn = new Dictionary<string, string> { ["Confirmed"] = "yes" },
        };
        var context = MakeContext(prompt: prompt);

        var result = await TemplateEngine.RenderAsync("{{check:Confirmed:yes,no}}", context);

        Assert.Equal("yes", result.Text);
        var field = Assert.IsType<CheckPromptField>(Assert.Single(prompt.LastFieldsShown!));
        Assert.Equal("yes", field.CheckedValue);
        Assert.Equal("no", field.UncheckedValue);
    }

    [Fact]
    public async Task UserCancelsPromptForm_WholeRenderIsCancelled_NotPartiallyResolved()
    {
        var prompt = new FakeInteractivePrompt { AnswersToReturn = null };
        var context = MakeContext(prompt: prompt);

        var result = await TemplateEngine.RenderAsync("Dear {{input:Name}},", context);

        Assert.True(result.Cancelled);
    }

    [Fact]
    public async Task NoPromptWired_InteractivePlaceholderResolvesToEmpty_DoesNotHang()
    {
        var result = await TemplateEngine.RenderAsync("Dear {{input:Name}},", MakeContext());

        Assert.False(result.Cancelled);
        Assert.Equal("Dear ,", result.Text);
    }
}
