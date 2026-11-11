using static MergedCells.Core.MapProblemKind;
using static MergedCells.Core.Tests.TestMaps;

namespace MergedCells.Core.Tests;

public class TeleporterValidationTests
{
    private static MapProblemKind[] Kinds(TeleporterPair[] pairs, Tile[,]? tiles = null) =>
        Map.Validate(ExampleRegions(), ExampleStart, ExampleEnd, tiles, pairs).Select(p => p.Kind).ToArray();

    [Fact]
    public void Pairs_on_plain_squares_off_the_start_and_end_are_valid()
    {
        Assert.Empty(Kinds([Pair(0, 1, 2, 2)]));
    }

    [Fact]
    public void Empty_pair_list_is_valid()
    {
        Assert.Empty(Kinds([]));
    }

    [Fact]
    public void Square_off_the_map_is_a_problem()
    {
        Assert.Equal(new[] { TeleporterOutsideMap }, Kinds([Pair(0, 1, 4, 0)]));
    }

    [Fact]
    public void Pair_with_both_squares_off_the_map_gives_two_problems()
    {
        Assert.Equal(new[] { TeleporterOutsideMap, TeleporterOutsideMap }, Kinds([Pair(-1, 0, 4, 4)]));
    }

    [Fact]
    public void Pair_on_one_square_is_a_problem()
    {
        Assert.Equal(new[] { TeleporterPairOnOneSquare }, Kinds([Pair(1, 1, 1, 1)]));
    }

    [Fact]
    public void Pair_on_one_square_off_the_map_is_reported_off_the_map_once()
    {
        Assert.Equal(new[] { TeleporterOutsideMap, TeleporterPairOnOneSquare }, Kinds([Pair(4, 0, 4, 0)]));
    }

    [Fact]
    public void Square_in_two_pairs_is_a_problem()
    {
        Assert.Equal(new[] { TeleporterSquareReused }, Kinds([Pair(0, 1, 2, 2), Pair(2, 2, 1, 1)]));
    }

    [Fact]
    public void Pair_on_one_square_used_by_an_earlier_pair_is_both()
    {
        Assert.Equal(new[] { TeleporterPairOnOneSquare, TeleporterSquareReused }, Kinds([Pair(0, 1, 2, 2), Pair(2, 2, 2, 2)]));
    }

    [Fact]
    public void Teleporter_on_the_start_is_a_problem()
    {
        Assert.Equal(new[] { TeleporterOnStart }, Kinds([Pair(0, 0, 1, 1)]));
    }

    [Fact]
    public void Teleporter_on_the_end_is_a_problem()
    {
        Assert.Equal(new[] { TeleporterOnEnd }, Kinds([Pair(3, 3, 1, 1)]));
    }

    [Theory]
    [InlineData(Tile.Wall)]
    [InlineData(Tile.Sticky)]
    [InlineData(Tile.Trap)]
    public void Teleporter_on_a_tile_is_a_problem(Tile tile)
    {
        var tiles = new Tile[4, 4];
        tiles[1, 1] = tile;

        Assert.Equal(new[] { TeleporterOnTile }, Kinds([Pair(1, 1, 2, 2)], tiles));
    }

    [Fact]
    public void Size_mismatch_skips_the_tile_check()
    {
        Assert.Equal(new[] { TilesSizeMismatch }, Kinds([Pair(1, 1, 2, 2)], Walls(3, 4, (1, 1))));
    }

    [Fact]
    public void Undefined_tile_under_a_teleporter_is_only_UnknownTile()
    {
        var tiles = new Tile[4, 4];
        tiles[1, 1] = (Tile)9;

        Assert.Equal(new[] { UnknownTile }, Kinds([Pair(1, 1, 2, 2)], tiles));
    }

    [Fact]
    public void Off_map_squares_are_skipped_by_the_later_kinds()
    {
        Assert.Equal(new[] { TeleporterOutsideMap }, Kinds([Pair(0, 1, 4, 0)], new Tile[4, 4]));
    }

    [Fact]
    public void Kinds_come_in_MapProblemKind_order()
    {
        var kinds = Kinds([Pair(0, 1, 4, 0), Pair(1, 2, 1, 2), Pair(0, 1, 0, 0), Pair(3, 3, 2, 1)], Walls(4, 4, (2, 1)));

        Assert.Equal(
            new[] { TeleporterOutsideMap, TeleporterPairOnOneSquare, TeleporterSquareReused, TeleporterOnStart, TeleporterOnEnd, TeleporterOnTile },
            kinds);
    }

    [Fact]
    public void Teleporter_problems_come_after_the_tile_ones()
    {
        Assert.Equal(new[] { TrapOnEnd, TeleporterOnStart }, Kinds([Pair(0, 0, 1, 1)], Traps(4, 4, (3, 3))));
    }

    [Fact]
    public void Problem_names_the_pair_and_the_square()
    {
        var problem = Assert.Single(Map.Validate(ExampleRegions(), ExampleStart, ExampleEnd, teleporters: [Pair(0, 1, 2, 2), Pair(1, 1, 4, 0)]));

        Assert.Contains("pair 1", problem.Message, StringComparison.Ordinal);
        Assert.Contains("[4, 0]", problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Empty_grid_with_pairs_reports_only_the_empty_grid()
    {
        var kinds = Map.Validate(new int[0, 0], new(0, 0), new(0, 0), teleporters: [Pair(0, 0, 1, 1)]).Select(p => p.Kind);

        Assert.Equal(new[] { EmptyGrid }, kinds);
    }

    [Fact]
    public void Create_throws_for_an_invalid_pair()
    {
        var ex = Assert.Throws<InvalidMapException>(
            () => Map.Create(ExampleRegions(), ExampleStart, ExampleEnd, teleporters: [Pair(0, 0, 1, 1)]));

        Assert.Equal(TeleporterOnStart, Assert.Single(ex.Problems).Kind);
    }
}
