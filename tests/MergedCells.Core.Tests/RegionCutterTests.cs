using AutoFixture;
using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

public class RegionCutterTests
{
    public static TheoryData<int> Sizes => new(10, 16);

    // 100 cuts, each from its own seed, with the end in the bottom-right corner unless given
    private static List<int[,]> Grids(int size, GeneratorConfig config, bool bends = false, Cell? end = null)
    {
        var at = end ?? new Cell(size - 1, size - 1);
        return new Fixture().CreateMany<ulong>(100).Select(seed => RegionCutter.Cut(size, at, config, bends, new GameRandom(seed))).ToList();
    }

    private static Map MapOf(int[,] grid, Cell? end = null) =>
        Map.Create(grid, new Cell(0, 0), end ?? new Cell(grid.GetLength(0) - 1, grid.GetLength(1) - 1));

    private static int SquaresIn(int[,] grid, int id) => grid.Cast<int>().Count(region => region == id);

    [Theory]
    [InlineData(10, 9, 9)]
    [InlineData(16, 15, 15)]
    [InlineData(10, 4, 6)]
    public void End_is_its_own_single_square(int size, int endRow, int endColumn)
    {
        var end = new Cell(endRow, endColumn);

        Assert.All(Grids(size, GeneratorConfigs.With(), end: end), grid => Assert.Equal(1, SquaresIn(grid, grid[endRow, endColumn])));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(10)]
    [InlineData(16)]
    [InlineData(40)]
    public void Grid_makes_a_valid_map(int size)
    {
        Assert.All(
            Grids(size, GeneratorConfigs.With()),
            grid => Assert.Empty(Map.Validate(grid, new Cell(0, 0), new Cell(size - 1, size - 1))));
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public void Regions_are_numbered_from_1_in_reading_order(int size)
    {
        Assert.All(Grids(size, GeneratorConfigs.With()), grid =>
        {
            var firstSeen = grid.Cast<int>().Distinct().ToArray();
            Assert.Equal(Enumerable.Range(1, firstSeen.Length), firstSeen);
        });
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public void Every_region_is_a_rectangle(int size)
    {
        Assert.All(Grids(size, GeneratorConfigs.With()), grid =>
        {
            var map = MapOf(grid);
            Assert.All(map.Rectangles, rectangle => Assert.True(map.IsRectangle(rectangle.Id)));
        });
    }

    // At size 10, 50% caps strips at 5 squares and 10% at the 2-square floor, below a block's longest side of 3: a block
    // squeezed to one square wide is a strip too, and keeps to the cap
    [Theory]
    [InlineData(50, 5)]
    [InlineData(10, 2)]
    public void No_strip_is_longer_than_the_cap(int maxStripPercent, int cap)
    {
        var strips = Grids(10, GeneratorConfigs.With(maxStripPercent: maxStripPercent, maxBlockSide: 3))
            .SelectMany(grid => MapOf(grid).Rectangles)
            .Where(rectangle => rectangle.Rows == 1 || rectangle.Columns == 1);

        Assert.All(strips, strip => Assert.InRange(Math.Max(strip.Rows, strip.Columns), 1, cap));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void No_block_side_passes_the_cap(int maxBlockSide)
    {
        var blocks = Grids(16, GeneratorConfigs.With(maxBlockSide: maxBlockSide))
            .SelectMany(grid => MapOf(grid).Rectangles)
            .Where(rectangle => rectangle.Rows > 1 && rectangle.Columns > 1);

        Assert.All(blocks, block => Assert.True(block.Rows <= maxBlockSide && block.Columns <= maxBlockSide, $"{block}"));
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public void Without_blocks_every_region_is_one_square_wide(int size)
    {
        var regions = Grids(size, GeneratorConfigs.With(blockWeight: 0)).SelectMany(grid => MapOf(grid).Rectangles);

        Assert.All(regions, region => Assert.Equal(1, Math.Min(region.Rows, region.Columns)));
    }

    // On a 2x2 map with the end bottom-right, the first strip takes whichever line it starts in, and the square left
    // over is boxed in by the end and the strip, so nothing joins it
    [Theory]
    [AutoData]
    public void Strips_across_run_along_rows(ulong seed)
    {
        var grid = RegionCutter.Cut(2, new Cell(1, 1), GeneratorConfigs.With(1, 0, 0, 0), false, new GameRandom(seed));

        Assert.Equal(grid[0, 0], grid[0, 1]);
    }

    [Theory]
    [AutoData]
    public void Strips_down_run_down_columns(ulong seed)
    {
        var grid = RegionCutter.Cut(2, new Cell(1, 1), GeneratorConfigs.With(0, 1, 0, 0), false, new GameRandom(seed));

        Assert.Equal(grid[0, 0], grid[1, 0]);
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public void With_only_single_squares_every_region_is_one_square(int size)
    {
        Assert.All(Grids(size, GeneratorConfigs.With(0, 0, 1, 0)), grid => Assert.Equal(size * size, grid.Cast<int>().Distinct().Count()));
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public void With_only_blocks_the_grid_is_valid(int size)
    {
        Assert.All(
            Grids(size, GeneratorConfigs.With(0, 0, 0, 1)),
            grid => Assert.Empty(Map.Validate(grid, new Cell(0, 0), new Cell(size - 1, size - 1))));
    }

    // With no single-square weight every single square but the end was squeezed, and none may be left beside a region
    // it could have joined: one square wide along that side, short enough to take it, and not the end
    [Theory]
    [MemberData(nameof(Sizes))]
    public void Squeezed_squares_are_folded_in(int size)
    {
        const int cap = 6;
        var config = GeneratorConfigs.With(singleWeight: 0, maxStripPercent: 100 * cap / size);
        Assert.All(Grids(size, config), grid =>
        {
            var map = MapOf(grid);
            var byId = map.Rectangles.ToDictionary(rectangle => rectangle.Id);
            foreach (var single in map.Rectangles.Where(rectangle => rectangle.Rows == 1 && rectangle.Columns == 1 && rectangle.TopLeft != map.End))
            {
                foreach (var direction in new[] { Direction.Up, Direction.Down, Direction.Left, Direction.Right })
                {
                    var next = single.TopLeft.Step(direction);
                    if (!map.Contains(next) || next == map.End)
                    {
                        continue;
                    }
                    var other = byId[map.RegionAt(next)];
                    var across = direction is Direction.Left or Direction.Right;
                    var linesUp = across ? other.Rows == 1 : other.Columns == 1;
                    Assert.False(linesUp && other.Rows * other.Columns + 1 <= cap, $"{single.TopLeft} could join {other}");
                }
            }
        });
    }

    // Squares per region that isn't a rectangle, over 100 bent 16x16 cuts
    private static List<(int[,] Grid, Map Map)> BentCuts(GeneratorConfig config) =>
        Grids(16, config, bends: true).Select(grid => (grid, MapOf(grid))).ToList();

    [Fact]
    public void Bends_appear_when_turned_on()
    {
        Assert.Contains(BentCuts(GeneratorConfigs.With()), cut => cut.Map.Rectangles.Any(region => !cut.Map.IsRectangle(region.Id)));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(8)]
    public void Bent_regions_stay_within_the_cap(int maxBendSquares)
    {
        Assert.All(BentCuts(GeneratorConfigs.With(maxBendSquares: maxBendSquares)), cut =>
        {
            foreach (var region in cut.Map.Rectangles.Where(region => !cut.Map.IsRectangle(region.Id)))
            {
                Assert.InRange(SquaresIn(cut.Grid, region.Id), 3, maxBendSquares);
            }
        });
    }

    // A 2x2 square inside one region is allowed only in a block, which is a rectangle
    [Fact]
    public void Bent_regions_are_one_square_wide()
    {
        Assert.All(BentCuts(GeneratorConfigs.With()), cut =>
        {
            var grid = cut.Grid;
            for (var row = 0; row + 1 < 16; row++)
            {
                for (var column = 0; column + 1 < 16; column++)
                {
                    var id = grid[row, column];
                    if (grid[row, column + 1] == id && grid[row + 1, column] == id && grid[row + 1, column + 1] == id)
                    {
                        Assert.True(cut.Map.IsRectangle(id), $"Region {id} holds a 2x2 square at [{row}, {column}]");
                    }
                }
            }
        });
    }

    [Fact]
    public void A_bend_chance_of_0_bends_nothing()
    {
        Assert.All(
            BentCuts(GeneratorConfigs.With(bendPercent: 0)),
            cut => Assert.All(cut.Map.Rectangles, region => Assert.True(cut.Map.IsRectangle(region.Id))));
    }

    [Fact]
    public void End_stays_its_own_single_square_with_bends()
    {
        Assert.All(BentCuts(GeneratorConfigs.With(bendPercent: 100)), cut => Assert.Equal(1, SquaresIn(cut.Grid, cut.Grid[15, 15])));
    }

    [Fact]
    public void Grid_with_bends_makes_a_valid_map()
    {
        Assert.All(
            Grids(16, GeneratorConfigs.With(bendPercent: 100), bends: true),
            grid => Assert.Empty(Map.Validate(grid, new Cell(0, 0), new Cell(15, 15))));
    }

    // The bend pass must draw no random numbers when bends are off, or the bend numbers would change plain maps
    [Theory]
    [AutoData]
    public void Without_bends_the_grid_ignores_the_bend_numbers(ulong seed)
    {
        Assert.Equal(
            RegionCutter.Cut(12, new Cell(11, 11), GeneratorConfigs.With(bendPercent: 0, maxBendSquares: 3), false, new GameRandom(seed)),
            RegionCutter.Cut(12, new Cell(11, 11), GeneratorConfigs.With(bendPercent: 100, maxBendSquares: 8), false, new GameRandom(seed)));
    }

    [Theory]
    [AutoData]
    public void One_seed_gives_one_grid(ulong seed)
    {
        var config = GeneratorConfigs.With();

        Assert.Equal(
            RegionCutter.Cut(12, new Cell(11, 10), config, false, new GameRandom(seed)),
            RegionCutter.Cut(12, new Cell(11, 10), config, false, new GameRandom(seed)));
    }
}
