using Snips.Core.Domain;

namespace Snips.Core.Repositories;

public interface ISnippetRepository
{
    /// <summary>
    /// Inserts a new snippet. <see cref="Snippet.Id"/>, <see cref="Snippet.CreatedUtc"/>, and
    /// <see cref="Snippet.ModifiedUtc"/> on the input are ignored and assigned by the repository.
    /// Throws <see cref="DuplicateSnippetNameException"/> if the name collides case-insensitively.
    /// </summary>
    Task<Snippet> CreateAsync(Snippet snippet, CancellationToken ct = default);

    Task<Snippet?> GetByIdAsync(string id, CancellationToken ct = default);

    Task<Snippet?> GetByNameAsync(string name, CancellationToken ct = default);

    /// <summary>Every snippet, most-recently-used first, then alphabetically. See SPEC.md §5.6.</summary>
    Task<IReadOnlyList<Snippet>> ListAsync(CancellationToken ct = default);

    /// <summary>
    /// Overwrites every column from the given snippet (looked up by Id) and refreshes ModifiedUtc.
    /// Throws <see cref="DuplicateSnippetNameException"/> if the new name collides with a different snippet.
    /// </summary>
    Task UpdateAsync(Snippet snippet, CancellationToken ct = default);

    /// <summary>Increments UseCount and sets LastUsedUtc to now — called when a snippet is applied.</summary>
    Task RecordUseAsync(string id, CancellationToken ct = default);

    /// <summary>Returns false if no row had that id.</summary>
    Task<bool> DeleteAsync(string id, CancellationToken ct = default);
}
