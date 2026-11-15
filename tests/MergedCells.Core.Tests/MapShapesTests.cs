using AutoFixture;

namespace MergedCells.Core.Tests;

public class MapShapesTests
{
    private const int Size = 6;
    private static readonly Cell End = new(Size - 1, Size - 1);

    // Every square its own region, so only the squares a test joins can break a rule
    private static int[,] Singles()
    {
        var grid = new int[Size, Size];
        for (var row = 0; row < Size; row++)
        {
            for (var column = 0; column < Size; column++)
            {
                grid[row, column] = row * Size + column + 1;
            }
        }
        return grid;
    }

    private static int[,] Joined(params (int Row, int Column)[] squares)
    {
        var grid = Singles();
        foreach (var (row, column) in squares)
        {
            grid[row, column] = grid[squares[0].Row, squares[0].Column];
        }
        return grid;
    }

    private static readonly (int, int)[] FiveSquareL = [(0, 0), (0, 1), (0, 2), (1, 0), (2, 0)];

    [Theory(Timeout = 30_000)]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Every_generated_map_fits(bool bends)
    {
        var config = GeneratorConfigs.With();
        var generator = new MapGenerator(config);

        var misfits = await Task.Run(
            () => new Fixture().CreateMany<ulong>(200)
                .Select((seed, i) => generator.Generate(new MapRequest(5 + i % 12, 1, 100, bends), seed))
                .OfType<GeneratedMap>()
                .Count(generated => !MapShapes.Fits(generated.Map.RegionGrid(), generated.Map.End, config, bends)),
            TestContext.Current.CancellationToken);

        Assert.Equal(0, misfits);
    }

    // At 6 squares a side, 50% gives a cap of 3
    [Fact]
    public void Strip_at_the_cap_fits()
    {
        Assert.True(MapShapes.Fits(Joined((0, 0), (0, 1), (0, 2)), End, GeneratorConfigs.With(maxStripPercent: 50), false));
    }

    [Fact]
    public void Strip_over_the_cap_does_not_fit()
    {
        Assert.False(MapShapes.Fits(Joined((0, 0), (0, 1), (0, 2), (0, 3)), End, GeneratorConfigs.With(maxStripPercent: 50), false));
    }

    [Fact]
    public void Block_side_over_the_maximum_does_not_fit()
    {
        var block = Joined((0, 0), (0, 1), (1, 0), (1, 1), (2, 0), (2, 1), (3, 0), (3, 1));

        Assert.False(MapShapes.Fits(block, End, GeneratorConfigs.With(maxBlockSide: 3), false));
    }

    [Fact]
    public void Bend_over_the_maximum_does_not_fit()
    {
        var bend = Joined((0, 0), (0, 1), (0, 2), (0, 3), (0, 4), (1, 0), (2, 0), (3, 0), (4, 0));

        Assert.False(MapShapes.Fits(bend, End, GeneratorConfigs.With(maxBendSquares: 8), true));
    }

    [Fact]
    public void Bend_holding_a_2x2_square_does_not_fit()
    {
        Assert.False(MapShapes.Fits(Joined((0, 0), (0, 1), (1, 0), (1, 1), (2, 0)), End, GeneratorConfigs.With(), true));
    }

    [Fact]
    public void Bend_without_bends_does_not_fit()
    {
        Assert.False(MapShapes.Fits(Joined(FiveSquareL), End, GeneratorConfigs.With(), false));
    }

    [Fact]
    public void Valid_bend_fits()
    {
        Assert.True(MapShapes.Fits(Joined(FiveSquareL), End, GeneratorConfigs.With(), true));
    }

    [Fact]
    public void Region_in_two_pieces_does_not_fit()
    {
        Assert.False(MapShapes.Fits(Joined((0, 0), (0, 2)), End, GeneratorConfigs.With(), true));
    }

    [Fact]
    public void End_that_is_not_its_own_square_does_not_fit()
    {
        Assert.False(MapShapes.Fits(Joined((Size - 1, Size - 2), (Size - 1, Size - 1)), End, GeneratorConfigs.With(), false));
    }
}
