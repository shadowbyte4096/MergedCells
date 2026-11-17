using static MergedCells.Core.Tests.TestMaps;

namespace MergedCells.Core.Tests;

public class PairsThatDontMatterTests
{
    private static readonly DifficultyRater Rater = new(DifficultyConfigs.With());

    // Level 32: right from the start enters [0, 2] and comes out of [2, 1] still sliding right; without it par is 4, not 2
    private static Map Teleport() =>
        Map.Create(Grid("AAAAA", "BBBBB", "CCCCD", "EEEEF"), new(0, 0), new(3, 4), teleporters: [Pair(0, 2, 2, 1)]);

    // Walls and traps shut every way to the end but right from the start, through [0, 1] and out of [2, 0]
    private static Tile[,] StrandingTiles()
    {
        var tiles = Walls(3, 3, (0, 2), (1, 2));
        tiles[1, 0] = Tile.Trap;
        tiles[1, 1] = Tile.Trap;
        return tiles;
    }

    private static readonly int[,] StrandingGrid = Grid("ABB", "ACC", "ADE");

    [Fact]
    public void Map_without_pairs_has_none()
    {
        Assert.Empty(TwistReliance.PairsThatDontMatter(Example(), Rater));
    }

    [Fact]
    public void Pair_on_the_only_quick_route_matters()
    {
        Assert.Empty(TwistReliance.PairsThatDontMatter(Teleport(), Rater));
    }

    [Fact]
    public void Pair_whose_removal_changes_nothing_is_listed()
    {
        Assert.Equal([1], TwistReliance.PairsThatDontMatter(IdleSecondPair(), Rater));
    }

    [Fact]
    public void Pair_whose_removal_strands_the_end_matters()
    {
        var map = Map.Create(StrandingGrid, new(0, 0), new(2, 1), StrandingTiles(), [Pair(2, 0, 0, 1)]);

        Assert.Empty(TwistReliance.PairsThatDontMatter(map, Rater));
    }

    [Fact]
    public void Idle_pairs_come_in_map_order()
    {
        var map = Map.Create(
            Grid("ABCD", "ABED", "AFFD", "AGGH"), new(1, 0), new(3, 3), teleporters: [Pair(2, 0, 0, 2), Pair(3, 0, 1, 2), Pair(0, 0, 1, 1)]);

        Assert.Equal([0, 2], TwistReliance.PairsThatDontMatter(map, Rater));
    }

    // Taking the walls and the trap off with the pair would leave the four measures as they are, so the pair would read
    // as idle
    [Fact]
    public void Tiles_stay_when_a_pair_is_taken_off()
    {
        var tiles = Walls(3, 3, (2, 0), (2, 1));
        tiles[1, 0] = Tile.Trap;
        var map = Map.Create(Grid("ABC", "ABC", "DDE"), new(0, 1), new(2, 2), tiles, [Pair(1, 1, 0, 0)]);

        Assert.Empty(TwistReliance.PairsThatDontMatter(map, Rater));
    }

    // The page passes the climb's own metrics rather than measuring the map again
    [Fact]
    public void Metrics_already_measured_give_the_same_list()
    {
        var map = IdleSecondPair();

        Assert.Equal(TwistReliance.PairsThatDontMatter(map, Rater), TwistReliance.PairsThatDontMatter(map, Rater.Measure(map)!, Rater));
    }

    [Fact]
    public void Unwinnable_map_throws()
    {
        var map = Map.Create(StrandingGrid, new(0, 0), new(2, 1), StrandingTiles());

        Assert.Throws<ArgumentException>(() => TwistReliance.PairsThatDontMatter(map, Rater));
    }
}
