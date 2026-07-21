namespace Snips.Core.Templates;

public abstract record PromptField(string Label);

public sealed record TextPromptField(string Label, string? Default) : PromptField(Label);

public sealed record MultilinePromptField(string Label) : PromptField(Label);

public sealed record ChoicePromptField(string Label, IReadOnlyList<string> Options) : PromptField(Label);

public sealed record DatePromptField(string Label, string Format) : PromptField(Label);

public sealed record CheckPromptField(string Label, string CheckedValue, string UncheckedValue) : PromptField(Label);

/// <summary>
/// Shows one form for every distinct interactive placeholder in a snippet (SPEC.md §7.6:
/// repeating the same label reuses one field, asked once). Implemented as a WPF dialog in
/// Snips.App; Core stays UI-free.
/// </summary>
public interface IInteractivePrompt
{
    /// <summary>Null return means the user cancelled (Esc) — the whole paste must be abandoned, not partially resolved.</summary>
    Task<IReadOnlyDictionary<string, string>?> ShowAsync(IReadOnlyList<PromptField> fields, CancellationToken ct = default);
}
