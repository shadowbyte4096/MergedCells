using static MergedCells.Core.Tests.TestMaps;

namespace MergedCells.Core.Tests;

public class TwistRelianceTests
{
    private static readonly DifficultyRater Rater = new(DifficultyConfigs.With());

    private static Level Shipped(int id) => FixtureLevels.ById(id);

    // A ring round the end: no slide stops beside it, so only the wall at [0, 2] makes [0, 1] a stop
    private static Map Ring(Tile[,]? tiles = null) => Map.Create(Grid("AAA", "ABA", "AAA"), new(0, 0), new(1, 1), tiles);

    [Fact]
    public void Plain_map_relies_on_nothing()
    {
        Assert.Equal(0, TwistReliance.Of(Example(), Rater));
    }

    // Wall: par 2, and 3 without the wall (par changes). Teleport: par 2, and 4 without the pair (par changes)
    [Theory]
    [InlineData(26, 1)]
    [InlineData(32, 1)]
    public void Reliance_counts_what_changes_without_the_twist(int id, int reliance)
    {
        Assert.Equal(reliance, TwistReliance.Of(Shipped(id).Map, Rater));
    }

    // The trap at [0, 3] catches Right from [0, 2], which no optimal route makes, and no tempting route either
    [Fact]
    public void A_twist_that_changes_nothing_gives_0()
    {
        var map = Map.Create(ExampleRegions(), ExampleStart, ExampleEnd, Traps(4, 4, (0, 3)));

        Assert.Equal(0, TwistReliance.Of(map, Rater));
    }

    [Fact]
    public void Counts_as_the_most_when_the_map_cannot_be_won_without_the_twist()
    {
        Assert.Equal(TwistReliance.Most, TwistReliance.Of(Ring(Walls(3, 3, (0, 2))), Rater));
    }

    [Fact]
    public void Unsolvable_map_throws()
    {
        Assert.Throws<ArgumentException>(() => TwistReliance.Of(Ring(), Rater));
    }

    // Level 26's wall is what makes par 2, and a trap at [1, 4] changes nothing
    [Fact]
    public void A_kind_that_changes_nothing_gives_0_even_when_another_kind_matters()
    {
        var wall = Shipped(26).Map;
        var tiles = wall.TileGrid();
        tiles[1, 4] = Tile.Trap;
        var map = Map.Create(wall.RegionGrid(), wall.Start, wall.End, tiles);

        Assert.Equal(0, TwistReliance.Of(map, Rater));
    }

    // Level 27's walls leave par at 3 but are what makes a tempting route a red herring: they add a decoy
    [Fact]
    public void A_twist_that_only_makes_a_tempting_route_counts()
    {
        Assert.True(TwistReliance.Of(Shipped(27).Map, Rater) >= 1);
    }
}
