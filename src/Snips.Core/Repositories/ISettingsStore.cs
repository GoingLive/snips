namespace Snips.Core.Repositories;

/// <summary>Key/value access to the Setting table (SPEC.md §4.3, §10).</summary>
public interface ISettingsStore
{
    Task<string?> GetAsync(string key, CancellationToken ct = default);

    Task SetAsync(string key, string value, CancellationToken ct = default);
}
