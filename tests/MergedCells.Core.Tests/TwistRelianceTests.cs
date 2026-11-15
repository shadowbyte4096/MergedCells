using static MergedCells.Core.Tests.TestMaps;

namespace MergedCells.Core.Tests;

public class TwistRelianceTests
{
    private static Level Shipped(int id) => ShippedCampaignTests.Campaign().Levels.Single(level => level.Id == id);

    // A ring round the end: no slide stops beside it, so only the wall at [0, 2] makes [0, 1] a stop
    private static Map Ring(Tile[,]? tiles = null) => Map.Create(Grid("AAA", "ABA", "AAA"), new(0, 0), new(1, 1), tiles);

    [Fact]
    public void Plain_map_relies_on_nothing()
    {
        Assert.Equal(0, TwistReliance.Of(Example()));
    }

    // Wall: par 2, and 3 without the wall. Teleport: par 2, and 4 without the pair
    [Theory]
    [InlineData(26, 1)]
    [InlineData(32, 2)]
    public void Reliance_is_the_size_of_the_par_change(int id, int reliance)
    {
        Assert.Equal(reliance, TwistReliance.Of(Shipped(id).Map));
    }

    // The trap at [0, 3] catches Right from [0, 2], which no optimal route makes
    [Fact]
    public void Twists_that_leave_par_alone_give_0()
    {
        var map = Map.Create(ExampleRegions(), ExampleStart, ExampleEnd, Traps(4, 4, (0, 3)));

        Assert.Equal(0, TwistReliance.Of(map));
    }

    [Fact]
    public void Null_when_the_map_without_twists_cannot_be_won()
    {
        Assert.Null(TwistReliance.Of(Ring(Walls(3, 3, (0, 2)))));
    }

    [Fact]
    public void Unsolvable_map_throws()
    {
        Assert.Throws<ArgumentException>(() => TwistReliance.Of(Ring()));
    }
}
