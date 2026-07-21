namespace Snips.Core.Templates;

/// <summary>Backs `{{counter:NAME}}` (SPEC.md §7.5). Persisted in the Counter table (Snips.Data).</summary>
public interface ICounterStore
{
    /// <summary>Adds step to the named counter (creating it at 0 first if new) and returns the new value.</summary>
    Task<long> IncrementAndGetAsync(string name, long step, CancellationToken ct = default);
}
