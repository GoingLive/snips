using Snips.Core.Id;

namespace Snips.Tests.Id;

public class SnowflakeIdGeneratorTests
{
    [Fact]
    public void NextId_IsZeroPaddedTo19Characters()
    {
        var generator = new SnowflakeIdGenerator();

        var id = generator.NextId();

        Assert.Equal(19, id.Length);
        Assert.True(long.TryParse(id, out _));
    }

    [Fact]
    public void NextId_IsMonotonicallyIncreasing()
    {
        var generator = new SnowflakeIdGenerator();

        var ids = Enumerable.Range(0, 10_000).Select(_ => generator.NextId()).ToList();

        for (var i = 1; i < ids.Count; i++)
        {
            Assert.True(
                string.CompareOrdinal(ids[i - 1], ids[i]) < 0,
                $"Expected {ids[i - 1]} < {ids[i]} at index {i}");
        }
    }

    [Fact]
    public void NextId_ProducesNoDuplicatesUnderConcurrency()
    {
        var generator = new SnowflakeIdGenerator();
        var bag = new System.Collections.Concurrent.ConcurrentBag<string>();

        Parallel.For(0, 20_000, _ => bag.Add(generator.NextId()));

        Assert.Equal(bag.Count, bag.Distinct().Count());
    }

    [Fact]
    public void Constructor_RejectsInstanceIdOutsideTenBits()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SnowflakeIdGenerator(1024));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SnowflakeIdGenerator(-1));
    }
}
