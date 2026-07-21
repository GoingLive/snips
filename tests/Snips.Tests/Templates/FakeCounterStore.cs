using Snips.Core.Templates;

namespace Snips.Tests.Templates;

public sealed class FakeCounterStore : ICounterStore
{
    private readonly Dictionary<string, long> _values = new(StringComparer.OrdinalIgnoreCase);

    public Task<long> IncrementAndGetAsync(string name, long step, CancellationToken ct = default)
    {
        _values.TryGetValue(name, out var current);
        var next = current + step;
        _values[name] = next;
        return Task.FromResult(next);
    }
}
