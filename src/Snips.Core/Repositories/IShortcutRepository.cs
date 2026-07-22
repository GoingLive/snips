using Snips.Core.Domain;

namespace Snips.Core.Repositories;

public interface IShortcutRepository
{
    Task<Shortcut?> GetBySnippetIdAsync(string snippetId, CancellationToken ct = default);

    Task<IReadOnlyList<Shortcut>> ListAllAsync(CancellationToken ct = default);

    /// <summary>Creates or overwrites the one shortcut a snippet may have. Throws
    /// <see cref="DuplicateShortcutException"/> if the combo is already used by a different snippet.</summary>
    Task<Shortcut> SetAsync(string snippetId, int modifiers, int virtualKey, CancellationToken ct = default);

    Task RemoveAsync(string snippetId, CancellationToken ct = default);
}
