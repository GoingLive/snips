using Snips.Core.Templates;

namespace Snips.Tests.Templates;

/// <summary>Records the fields it was asked to show, and returns canned answers (or cancels).</summary>
public sealed class FakeInteractivePrompt : IInteractivePrompt
{
    public IReadOnlyList<PromptField>? LastFieldsShown { get; private set; }
    public IReadOnlyDictionary<string, string>? AnswersToReturn { get; set; } = new Dictionary<string, string>();

    public Task<IReadOnlyDictionary<string, string>?> ShowAsync(IReadOnlyList<PromptField> fields, CancellationToken ct = default)
    {
        LastFieldsShown = fields;
        return Task.FromResult(AnswersToReturn);
    }
}
