using System.Windows;
using Snips.Core.Templates;

namespace Snips.App;

/// <summary>Must be constructed and used from the UI thread — ShowDialog() requires it, and
/// every caller in this app (triggered from the picker's own event handlers) already is.</summary>
public sealed class WpfInteractivePrompt(Window owner) : IInteractivePrompt
{
    public Task<IReadOnlyDictionary<string, string>?> ShowAsync(
        IReadOnlyList<PromptField> fields, CancellationToken ct = default)
    {
        var dialog = new InteractivePromptWindow(fields) { Owner = owner };
        var result = dialog.ShowDialog();
        return Task.FromResult<IReadOnlyDictionary<string, string>?>(result == true ? dialog.GetAnswers() : null);
    }
}
