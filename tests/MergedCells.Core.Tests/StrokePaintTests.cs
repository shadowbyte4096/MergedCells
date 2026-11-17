namespace MergedCells.Core.Tests;

// A finished stroke goes straight into an edit, as the editor does it: whatever a finger does, the path must stay one
// joined chain of squares
public class StrokePaintTests
{
    private const float Slop = 14;

    // Each step is 64 units, one square, so every drag is past the slop
    private static StrokeEnd Walk(params (int Row, int Column)?[] squares)
    {
        var stroke = new EditStroke(Slop);
        var first = squares[0]!.Value;
        stroke.Press(0, first.Column * 64, first.Row * 64, new Cell(first.Row, first.Column));
        var step = 1;
        foreach (var square in squares.Skip(1))
        {
            var cell = square is { } at ? new Cell(at.Row, at.Column) : (Cell?)null;
            stroke.Drag(0, step * 64, step * 64, cell);
            step++;
        }
        return stroke.Release(0)!;
    }

    public static TheoryData<(int, int)?[]> Walks => new()
    {
        // Overshoot, come back one square, then turn down
        new (int, int)?[] { (0, 0), (0, 1), (0, 2), (0, 1), (1, 1) },
        // Wobble back across a square edge while turning
        new (int, int)?[] { (0, 0), (0, 1), (1, 1), (0, 1), (0, 2), (1, 2) },
        // Leave the board at the top, come back in two squares to the left, then move down
        new (int, int)?[] { (0, 1), (0, 2), null, (0, 0), (1, 0) },
    };

    [Theory]
    [MemberData(nameof(Walks))]
    public void Every_square_of_a_stroke_shares_an_edge_with_the_one_before(params (int, int)?[] walk)
    {
        var cells = Walk(walk).Cells;

        for (var index = 1; index < cells.Count; index++)
        {
            Assert.Equal(1, Math.Abs(cells[index].Row - cells[index - 1].Row) + Math.Abs(cells[index].Column - cells[index - 1].Column));
        }
    }

    [Theory]
    [MemberData(nameof(Walks))]
    public void Any_stroke_paints_its_squares_into_one_block(params (int, int)?[] walk)
    {
        var end = Walk(walk);

        var painted = LevelEdits.Paint(LevelEdits.Blank(4, 4), end.Cells).Map;

        Assert.Single(end.Cells.Select(painted.RegionAt).Distinct());
    }
}
