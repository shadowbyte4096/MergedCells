using AutoFixture;

namespace MergedCells.Core.Tests;

public class CellRangeTests
{
    [Theory]
    [InlineData(1.5, 2.25, 3.5, 4.75, 10, 10, 2, 1, 4, 3)]
    [InlineData(1.0, 2.0, 3.0, 4.0, 10, 10, 2, 1, 3, 2)]
    [InlineData(-5, -5, 50, 50, 4, 6, 0, 0, 3, 5)]
    [InlineData(-2.5, -1, 1.5, 2, 4, 6, 0, 0, 1, 1)]
    [InlineData(4.5, 2.5, 9, 9, 4, 6, 2, 4, 3, 5)]
    [InlineData(0, 0, 2, 7.5, 8, 3, 0, 0, 7, 1)]
    public void Covering_gives_the_cells_a_view_touches(
        double left, double top, double right, double bottom, int rows, int columns,
        int expectedTop, int expectedLeft, int expectedBottom, int expectedRight)
    {
        var range = CellRange.Covering(left, top, right, bottom, rows, columns);

        Assert.Equal(new CellRange(expectedTop, expectedLeft, expectedBottom, expectedRight), range);
    }

    [Theory]
    [InlineData(-3, 0, 0, 2)]
    [InlineData(6, 0, 9, 2)]
    [InlineData(0, -3, 2, 0)]
    [InlineData(0, 4, 2, 7)]
    public void Covering_a_view_that_misses_the_map_is_null(double left, double top, double right, double bottom)
    {
        Assert.Null(CellRange.Covering(left, top, right, bottom, 4, 6));
    }

    // The rectangle covers rows 2-3 and columns 3-5
    [Theory]
    [InlineData(0, 0, 2, 3, true)]
    [InlineData(2, 3, 3, 5, true)]
    [InlineData(0, 0, 9, 9, true)]
    [InlineData(3, 5, 3, 5, true)]
    [InlineData(2, 6, 3, 8, false)]
    [InlineData(4, 3, 5, 5, false)]
    [InlineData(0, 0, 1, 9, false)]
    [InlineData(0, 0, 9, 2, false)]
    public void Overlaps_when_any_square_is_shared(int top, int left, int bottom, int right, bool expected)
    {
        var rectangle = new Rectangle(new Fixture().Create<int>(), new(2, 3), 2, 3);

        Assert.Equal(expected, rectangle.Overlaps(new CellRange(top, left, bottom, right)));
    }
}
