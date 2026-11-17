using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

public class CellPathTests
{
    [Fact]
    public void Same_square_is_one_square()
    {
        Assert.Equal([new Cell(2, 3)], CellPath.Line(new(2, 3), new(2, 3)));
    }

    [Theory]
    [InlineData(2, 5)]
    [InlineData(2, 0)]
    [InlineData(0, 2)]
    [InlineData(5, 2)]
    public void Straight_line_is_every_square_between(int toRow, int toColumn)
    {
        var from = new Cell(2, 2);
        var to = new Cell(toRow, toColumn);
        var steps = Math.Abs(toRow - 2) + Math.Abs(toColumn - 2);

        var expected = Enumerable.Range(0, steps + 1)
            .Select(i => new Cell(2 + i * Math.Sign(toRow - 2), 2 + i * Math.Sign(toColumn - 2)));

        Assert.Equal(expected, CellPath.Line(from, to));
    }

    [Theory]
    [AutoData]
    public void Each_square_shares_an_edge_with_the_last(ulong seed)
    {
        var random = new GameRandom(seed);
        for (var line = 0; line < 200; line++)
        {
            var from = new Cell(random.Next(30), random.Next(30));
            var to = new Cell(random.Next(30), random.Next(30));

            var path = CellPath.Line(from, to);

            Assert.Equal(from, path[0]);
            Assert.Equal(to, path[^1]);
            Assert.Equal(Math.Abs(to.Row - from.Row) + Math.Abs(to.Column - from.Column) + 1, path.Count);
            for (var step = 1; step < path.Count; step++)
            {
                Assert.Equal(1, Math.Abs(path[step].Row - path[step - 1].Row) + Math.Abs(path[step].Column - path[step - 1].Column));
            }
        }
    }

    [Fact]
    public void Diagonal_tie_steps_horizontally_first()
    {
        Assert.Equal([new Cell(0, 0), new Cell(0, 1), new Cell(1, 1)], CellPath.Line(new(0, 0), new(1, 1)));
    }
}
