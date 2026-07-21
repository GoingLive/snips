namespace Snips.Core.Templates;

public abstract record TemplateSegment;

public sealed record LiteralSegment(string Text) : TemplateSegment;

public sealed record FilterSpec(string Name, IReadOnlyList<string> Args);

public sealed record PlaceholderSegment(string Name, IReadOnlyList<string> Args, IReadOnlyList<FilterSpec> Filters)
    : TemplateSegment;
