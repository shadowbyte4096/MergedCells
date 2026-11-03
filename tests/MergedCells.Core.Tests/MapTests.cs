using static MergedCells.Core.Tests.TestMaps;

namespace MergedCells.Core.Tests;

public class MapTests
{
    private static readonly int[,] Ring = Grid("AAAB", "ACAB", "AAAB", "DDDE");

    // RegionAt returns the ID values, so this grid uses numbers
    private static Map TwoByThree() => Map.Create(new[,] { { 1, 2, 3 }, { 4, 5, 6 } }, new(0, 0), new(1, 2));

    [Fact]
    public void Create_throws_with_the_problems_Validate_reports()
    {
        var ex = Assert.Throws<InvalidMapException>(() => Map.Create(Ring, new(5, 5), new(3, 3)));

        Assert.Equal(Map.Validate(Ring, new(5, 5), new(3, 3)), ex.Problems);
    }

    [Fact]
    public void Create_exception_message_lists_every_problem()
    {
        var ex = Assert.Throws<InvalidMapException>(() => Map.Create(Ring, new(5, 5), new(3, 3)));

        Assert.All(ex.Problems, p => Assert.Contains(p.Message, ex.Message));
    }

    [Fact]
    public void Create_with_null_grid_throws()
    {
        Assert.Throws<ArgumentNullException>(() => Map.Create(null!, new(0, 0), new(1, 1)));
    }

    [Fact]
    public void Create_copies_the_grid()
    {
        var regions = ExampleRegions();
        var map = Map.Create(regions, ExampleStart, ExampleEnd);

        regions[0, 0] = 99;

        Assert.Equal(1, map.RegionAt(new(0, 0)));
    }

    [Fact]
    public void Start_and_end_are_kept()
    {
        var map = Example();

        Assert.Equal((new Cell(0, 0), new Cell(3, 3)), (map.Start, map.End));
    }

    [Fact]
    public void Size_comes_from_the_grid()
    {
        var map = TwoByThree();

        Assert.Equal((2, 3), (map.Rows, map.Columns));
    }

    [Theory]
    [InlineData(0, 0, 1)]
    [InlineData(0, 2, 3)]
    [InlineData(1, 0, 4)]
    [InlineData(1, 2, 6)]
    public void RegionAt_reads_row_then_column(int row, int column, int expected)
    {
        Assert.Equal(expected, TwoByThree().RegionAt(new(row, column)));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(2, 0)]
    [InlineData(0, 3)]
    public void RegionAt_outside_the_map_throws(int row, int column)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TwoByThree().RegionAt(new(row, column)));
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(1, 2, true)]
    [InlineData(-1, 0, false)]
    [InlineData(0, -1, false)]
    [InlineData(2, 0, false)]
    [InlineData(0, 3, false)]
    public void Contains_is_true_only_inside_the_grid(int row, int column, bool expected)
    {
        Assert.Equal(expected, TwoByThree().Contains(new(row, column)));
    }
}
