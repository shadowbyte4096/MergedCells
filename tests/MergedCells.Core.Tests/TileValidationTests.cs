using static MergedCells.Core.MapProblemKind;
using static MergedCells.Core.Tests.TestMaps;

namespace MergedCells.Core.Tests;

public class TileValidationTests
{
    private const Tile Undefined = (Tile)9;

    private static MapProblemKind[] Kinds(int[,] regions, Cell start, Cell end, Tile[,] tiles) =>
        Map.Validate(regions, start, end, tiles).Select(p => p.Kind).ToArray();

    private static MapProblemKind[] ExampleKinds(Tile[,] tiles) => Kinds(ExampleRegions(), ExampleStart, ExampleEnd, tiles);

    [Fact]
    public void Walls_off_the_start_and_end_are_valid()
    {
        Assert.Empty(ExampleKinds(Walls(4, 4, (1, 1), (0, 3))));
    }

    [Theory]
    [InlineData(3, 4)]
    [InlineData(4, 3)]
    public void Tiles_of_a_different_size_are_a_problem(int rows, int columns)
    {
        Assert.Equal(new[] { TilesSizeMismatch }, ExampleKinds(new Tile[rows, columns]));
    }

    [Fact]
    public void Size_mismatch_skips_the_other_tile_checks()
    {
        Assert.Equal(new[] { TilesSizeMismatch }, ExampleKinds(new[,] { { Undefined } }));
    }

    [Fact]
    public void Undefined_tile_value_is_a_problem()
    {
        var tiles = Walls(4, 4);
        tiles[2, 1] = Undefined;

        Assert.Equal(new[] { UnknownTile }, ExampleKinds(tiles));
    }

    [Fact]
    public void Undefined_tile_message_names_the_first_square()
    {
        var tiles = Walls(4, 4);
        tiles[2, 1] = Undefined;
        tiles[3, 0] = Undefined;

        var problem = Assert.Single(Map.Validate(ExampleRegions(), ExampleStart, ExampleEnd, tiles));

        Assert.Contains("[2, 1]", problem.Message);
    }

    [Fact]
    public void Wall_on_the_start_is_a_problem()
    {
        Assert.Equal(new[] { WallOnStart }, ExampleKinds(Walls(4, 4, (0, 0))));
    }

    [Fact]
    public void Wall_on_the_end_is_a_problem()
    {
        Assert.Equal(new[] { WallOnEnd }, ExampleKinds(Walls(4, 4, (3, 3))));
    }

    [Fact]
    public void Tile_problems_come_after_the_existing_ones()
    {
        var kinds = Kinds(AllSingles3x3(), new(0, 2), new(2, 0), Walls(3, 3, (0, 2), (2, 0)));

        Assert.Equal(new[] { StartRightOfEnd, WallOnStart, WallOnEnd }, kinds);
    }

    [Fact]
    public void Walls_are_not_checked_for_a_start_and_end_off_the_map()
    {
        var kinds = Kinds(AllSingles3x3(), new(5, 5), new(6, 6), Walls(3, 3, (1, 1)));

        Assert.Equal(new[] { StartOutsideMap, EndOutsideMap }, kinds);
    }

    [Fact]
    public void Empty_grid_with_tiles_reports_only_the_empty_grid()
    {
        Assert.Equal(new[] { EmptyGrid }, Kinds(new int[0, 0], new(0, 0), new(0, 0), new Tile[2, 2]));
    }

    [Fact]
    public void Create_throws_for_a_wall_on_the_start()
    {
        var ex = Assert.Throws<InvalidMapException>(
            () => Map.Create(ExampleRegions(), ExampleStart, ExampleEnd, Walls(4, 4, (0, 0))));

        Assert.Equal(WallOnStart, Assert.Single(ex.Problems).Kind);
    }
}
