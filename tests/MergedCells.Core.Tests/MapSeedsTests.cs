using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

public class MapSeedsTests
{
    // Computed by hand: ticks × 0x9E3779B97F4A7C15 mod 2^64, plus the counter
    [Theory]
    [InlineData(1L, 0, 11400714819323198485UL)]
    [InlineData(2L, 3, 4354685564936845357UL)]
    public void Seed_is_pinned(long ticks, int counter, ulong expected)
    {
        Assert.Equal(expected, MapSeeds.From(ticks, counter));
    }

    [Theory]
    [AutoData]
    public void Different_counters_give_different_seeds(long ticks)
    {
        Assert.NotEqual(MapSeeds.From(ticks, 0), MapSeeds.From(ticks, 1));
    }
}
