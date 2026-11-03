using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

public class CellTests
{
    [Theory]
    [InlineAutoData(Direction.Up, -1, 0)]
    [InlineAutoData(Direction.Down, 1, 0)]
    [InlineAutoData(Direction.Left, 0, -1)]
    [InlineAutoData(Direction.Right, 0, 1)]
    public void Step_moves_one_square_in_the_direction(
        Direction direction, int rowChange, int columnChange, int row, int column)
    {
        var next = new Cell(row, column).Step(direction);

        Assert.Equal(new Cell(row + rowChange, column + columnChange), next);
    }

    [Theory]
    [AutoData]
    public void Step_with_an_undefined_direction_throws(int row, int column)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Cell(row, column).Step((Direction)(-1)));
    }
}
