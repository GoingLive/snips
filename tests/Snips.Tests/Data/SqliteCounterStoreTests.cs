namespace Snips.Tests.Data;

public class SqliteCounterStoreTests : IAsyncLifetime
{
    private readonly TempDatabaseFixture _fixture = new();

    public Task InitializeAsync() => _fixture.InitializeAsync();
    public Task DisposeAsync() => _fixture.DisposeAsync();

    [Fact]
    public async Task IncrementAndGetAsync_NewCounter_StartsFromStep()
    {
        var value = await _fixture.Database.Counters.IncrementAndGetAsync("Invoice", 1);
        Assert.Equal(1, value);
    }

    [Fact]
    public async Task IncrementAndGetAsync_ExistingCounter_AddsStep()
    {
        await _fixture.Database.Counters.IncrementAndGetAsync("Invoice", 1);
        await _fixture.Database.Counters.IncrementAndGetAsync("Invoice", 1);
        var third = await _fixture.Database.Counters.IncrementAndGetAsync("Invoice", 5);

        Assert.Equal(7, third);
    }

    [Fact]
    public async Task IncrementAndGetAsync_IsCaseInsensitiveByName()
    {
        await _fixture.Database.Counters.IncrementAndGetAsync("Invoice", 1);
        var value = await _fixture.Database.Counters.IncrementAndGetAsync("INVOICE", 1);

        Assert.Equal(2, value);
    }

    [Fact]
    public async Task IncrementAndGetAsync_DifferentCounters_AreIndependent()
    {
        await _fixture.Database.Counters.IncrementAndGetAsync("A", 10);
        var b = await _fixture.Database.Counters.IncrementAndGetAsync("B", 1);

        Assert.Equal(1, b);
    }
}
