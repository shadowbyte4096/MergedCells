using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

public class GameRandomTests
{
    // pcg32-demo's first outputs for pcg32_srandom(42, 54), the stream GameRandom fixes
    [Fact]
    public void Seed_42_gives_the_published_PCG32_outputs()
    {
        var random = new GameRandom(42);

        var outputs = Enumerable.Range(0, 6).Select(_ => random.NextUInt()).ToArray();

        Assert.Equal([0xa15c02b7u, 0x7b47f409u, 0xba1d3330u, 0x83d2f293u, 0xbfa4784bu, 0xcbed606eu], outputs);
    }

    [Theory]
    [AutoData]
    public void One_seed_gives_one_sequence(ulong seed)
    {
        var first = new GameRandom(seed);
        var second = new GameRandom(seed);

        Assert.Equal(
            Enumerable.Range(0, 20).Select(_ => first.NextUInt()).ToArray(),
            Enumerable.Range(0, 20).Select(_ => second.NextUInt()).ToArray());
    }

    [Theory]
    [InlineAutoData(1)]
    [InlineAutoData(2)]
    [InlineAutoData(3)]
    [InlineAutoData(7)]
    [InlineAutoData(1000)]
    [InlineAutoData(int.MaxValue)]
    public void Next_stays_below_its_bound(int bound, ulong seed)
    {
        var random = new GameRandom(seed);

        var draws = Enumerable.Range(0, 1000).Select(_ => random.Next(bound)).ToArray();

        Assert.All(draws, draw => Assert.InRange(draw, 0, bound - 1));
    }

    [Theory]
    [AutoData]
    public void Next_reaches_every_value_of_a_small_range(ulong seed)
    {
        var random = new GameRandom(seed);

        var seen = Enumerable.Range(0, 500).Select(_ => random.Next(5)).Distinct().Order().ToArray();

        Assert.Equal([0, 1, 2, 3, 4], seen);
    }

    [Theory]
    [AutoData]
    public void Next_between_stays_in_its_range(ulong seed)
    {
        var random = new GameRandom(seed);

        var draws = Enumerable.Range(0, 1000).Select(_ => random.Next(-3, 4)).ToArray();

        Assert.All(draws, draw => Assert.InRange(draw, -3, 3));
    }

    [Theory]
    [InlineAutoData(0)]
    [InlineAutoData(-1)]
    public void Next_rejects_a_bound_below_one(int bound, ulong seed)
    {
        var random = new GameRandom(seed);

        Assert.Throws<ArgumentOutOfRangeException>(() => random.Next(bound));
    }

    [Theory]
    [InlineAutoData(3, 3)]
    [InlineAutoData(4, 3)]
    public void Next_between_rejects_an_empty_range(int min, int max, ulong seed)
    {
        var random = new GameRandom(seed);

        Assert.Throws<ArgumentOutOfRangeException>(() => random.Next(min, max));
    }

    [Theory]
    [AutoData]
    public void Shuffle_gives_a_permutation(ulong seed)
    {
        var items = Enumerable.Range(0, 50).ToList();

        new GameRandom(seed).Shuffle(items);

        Assert.Equal(Enumerable.Range(0, 50), items.Order());
    }
}
