using System.Text;

namespace Snips.Core.Templates;

/// <summary>Result of rendering a snippet body. Cancelled is distinct from an empty successful
/// render so the caller (the paste pipeline) knows to abandon the whole operation rather than
/// paste an empty string — SPEC.md §7.6: "Esc in the form cancels the whole operation."</summary>
public sealed record TemplateRenderResult(bool Cancelled, string Text)
{
    public static readonly TemplateRenderResult CancelledResult = new(true, string.Empty);
}

public static class TemplateEngine
{
    private static readonly HashSet<string> InteractiveNames =
        new(StringComparer.OrdinalIgnoreCase) { "input", "multiline", "choice", "datepick", "check" };

    public static async Task<TemplateRenderResult> RenderAsync(
        string source, TemplateContext context, CancellationToken ct = default)
    {
        var segments = TemplateParser.Parse(source);
        var interactiveFields = CollectInteractiveFields(segments);

        IReadOnlyDictionary<string, string> promptAnswers = new Dictionary<string, string>();
        if (interactiveFields.Count > 0)
        {
            if (context.Prompt is null)
            {
                // No prompt UI wired (e.g. rendering a preview with no dialog owner) — every
                // interactive placeholder resolves to empty rather than blocking indefinitely.
                promptAnswers = interactiveFields.ToDictionary(f => f.Label, _ => string.Empty);
            }
            else
            {
                var answers = await context.Prompt.ShowAsync(interactiveFields, ct);
                if (answers is null)
                    return TemplateRenderResult.CancelledResult;
                promptAnswers = answers;
            }
        }

        var builder = new StringBuilder();
        foreach (var segment in segments)
        {
            switch (segment)
            {
                case LiteralSegment literal:
                    builder.Append(literal.Text);
                    break;
                case PlaceholderSegment placeholder:
                    var value = await ResolvePlaceholderAsync(placeholder, context, promptAnswers, ct);
                    builder.Append(ApplyFilters(value, placeholder.Filters));
                    break;
            }
        }

        return new TemplateRenderResult(Cancelled: false, builder.ToString());
    }

    private static List<PromptField> CollectInteractiveFields(IReadOnlyList<TemplateSegment> segments)
    {
        var fields = new List<PromptField>();
        var seenLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var segment in segments)
        {
            if (segment is not PlaceholderSegment ph || !InteractiveNames.Contains(ph.Name) || ph.Args.Count == 0)
                continue;

            var label = ph.Args[0];
            if (!seenLabels.Add(label))
                continue; // repeated label reuses the first field — SPEC.md §7.6

            fields.Add(BuildField(ph.Name.ToLowerInvariant(), label, ph.Args));
        }

        return fields;
    }

    private static PromptField BuildField(string kind, string label, IReadOnlyList<string> args) => kind switch
    {
        "input" => new TextPromptField(label, args.Count > 1 ? args[1] : null),
        "multiline" => new MultilinePromptField(label),
        "choice" => new ChoicePromptField(label, args.Count > 1 ? args[1].Split(',') : []),
        "datepick" => new DatePromptField(label, args.Count > 1 ? args[1] : "yyyy-MM-dd"),
        "check" => BuildCheckField(label, args),
        _ => new TextPromptField(label, null),
    };

    private static CheckPromptField BuildCheckField(string label, IReadOnlyList<string> args)
    {
        var values = args.Count > 1 ? args[1].Split(',', 2) : [];
        var checkedValue = values.Length > 0 ? values[0] : "yes";
        var uncheckedValue = values.Length > 1 ? values[1] : "no";
        return new CheckPromptField(label, checkedValue, uncheckedValue);
    }

    private static async ValueTask<string> ResolvePlaceholderAsync(
        PlaceholderSegment placeholder, TemplateContext context,
        IReadOnlyDictionary<string, string> promptAnswers, CancellationToken ct)
    {
        if (InteractiveNames.Contains(placeholder.Name) && placeholder.Args.Count > 0)
        {
            return promptAnswers.TryGetValue(placeholder.Args[0], out var answer) ? answer : string.Empty;
        }

        var builtIn = await BuiltInVariables.TryResolveAsync(placeholder.Name, placeholder.Args, context, ct);
        if (builtIn is not null)
            return builtIn;

        if (context.ExternalVariables is not null)
        {
            // Case-insensitive regardless of what comparer the caller's dictionary happens to
            // use — every other variable-name lookup in the engine is case-insensitive, and an
            // external-variables.json author shouldn't need to know or care that this one's
            // different. The map is expected to be tiny, so a linear scan costs nothing real.
            foreach (var (key, value) in context.ExternalVariables)
            {
                if (string.Equals(key, placeholder.Name, StringComparison.OrdinalIgnoreCase))
                    return value;
            }
        }

        // Unknown name (typo, or a Phase-5 user-defined/script variable not implemented yet):
        // leave the placeholder text visible rather than silently dropping it — SPEC.md §1.1.
        return Reconstruct(placeholder);
    }

    private static string Reconstruct(PlaceholderSegment placeholder)
    {
        var args = placeholder.Args.Count > 0 ? ":" + string.Join(':', placeholder.Args) : string.Empty;
        return $"{{{{{placeholder.Name}{args}}}}}";
    }

    private static string ApplyFilters(string value, IReadOnlyList<FilterSpec> filters)
    {
        foreach (var filter in filters)
            value = TemplateFilters.Apply(filter.Name, value, filter.Args);

        return value;
    }
}
