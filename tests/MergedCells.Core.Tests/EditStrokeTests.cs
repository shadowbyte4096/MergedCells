namespace MergedCells.Core.Tests;

public class EditStrokeTests
{
    private const float Slop = 14;

    private static EditStroke PressedAt(Cell cell, float x = 0, float y = 0)
    {
        var stroke = new EditStroke(Slop);
        stroke.Press(0, x, y, cell);
        return stroke;
    }

    [Fact]
    public void Press_on_the_board_starts_a_stroke()
    {
        var stroke = PressedAt(new(1, 1));

        Assert.True(stroke.IsActive);
        Assert.Equal([new Cell(1, 1)], stroke.Cells);
    }

    [Fact]
    public void Press_off_the_board_starts_nothing()
    {
        var stroke = new EditStroke(Slop);

        stroke.Press(0, 0, 0, null);

        Assert.False(stroke.IsActive);
    }

    [Fact]
    public void Release_inside_the_slop_is_a_tap_even_across_a_square_edge()
    {
        var stroke = PressedAt(new(0, 0), 60, 10);
        stroke.Drag(0, 70, 10, new(0, 1));

        var end = stroke.Release(0);

        Assert.NotNull(end);
        Assert.True(end.IsTap);
        Assert.Equal([new Cell(0, 0)], end.Cells);
    }

    [Fact]
    public void Release_past_the_slop_is_a_drag_with_its_path()
    {
        var stroke = PressedAt(new(0, 0));
        stroke.Drag(0, 64, 0, new(0, 1));
        stroke.Drag(0, 128, 0, new(0, 2));

        var end = stroke.Release(0);

        Assert.NotNull(end);
        Assert.False(end.IsTap);
        Assert.Equal([new Cell(0, 0), new Cell(0, 1), new Cell(0, 2)], end.Cells);
    }

    [Fact]
    public void Fast_drag_fills_the_skipped_squares()
    {
        var stroke = PressedAt(new(0, 0));

        var added = stroke.Drag(0, 0, 128, new(2, 0));

        Assert.Equal([new Cell(1, 0), new Cell(2, 0)], added);
    }

    [Fact]
    public void Squares_already_in_the_stroke_are_not_added_again()
    {
        var stroke = PressedAt(new(0, 0));
        stroke.Drag(0, 64, 0, new(0, 1));
        stroke.Drag(0, 0, 0, new(0, 0));
        stroke.Drag(0, 64, 0, new(0, 1));

        Assert.Equal([new Cell(0, 0), new Cell(0, 1)], stroke.Cells);
    }

    [Fact]
    public void Drag_off_the_board_adds_nothing()
    {
        var stroke = PressedAt(new(0, 0));

        Assert.Empty(stroke.Drag(0, -64, 0, null));
        Assert.Equal([new Cell(0, 0)], stroke.Cells);
    }

    [Fact]
    public void Re_entering_the_board_joins_the_path()
    {
        var stroke = PressedAt(new(0, 0));
        stroke.Drag(0, 64, 0, new(0, 1));
        stroke.Drag(0, 64, -64, null);

        var added = stroke.Drag(0, 192, 0, new(0, 3));

        Assert.Equal([new Cell(0, 2), new Cell(0, 3)], added);
    }

    [Fact]
    public void Second_finger_cancels_until_every_finger_lifts()
    {
        var stroke = PressedAt(new(0, 0));
        stroke.Press(1, 64, 64, new(1, 1));
        Assert.Null(stroke.Release(0));
        stroke.Press(0, 128, 128, new(2, 2));
        Assert.False(stroke.IsActive);

        stroke.Release(1);
        stroke.Release(0);
        stroke.Press(0, 0, 0, new(0, 0));

        Assert.True(stroke.IsActive);
    }

    [Fact]
    public void Press_of_a_finger_already_down_drops_the_stroke()
    {
        var stroke = PressedAt(new(0, 0));
        stroke.Drag(0, 64, 0, new(0, 1));

        stroke.Press(0, 128, 128, new(2, 2));

        Assert.Equal([new Cell(2, 2)], stroke.Cells);
    }

    [Fact]
    public void Reset_forgets_everything()
    {
        var stroke = PressedAt(new(0, 0));
        stroke.Press(1, 64, 64, new(1, 1));

        stroke.Reset();
        stroke.Press(0, 0, 0, new(1, 1));

        Assert.True(stroke.IsActive);
    }

    [Fact]
    public void Other_fingers_drags_are_ignored()
    {
        var stroke = PressedAt(new(0, 0));

        Assert.Empty(stroke.Drag(1, 128, 0, new(0, 2)));
        Assert.Equal([new Cell(0, 0)], stroke.Cells);
    }

    [Theory]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void Slop_must_be_zero_or_more_and_finite(float slop)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EditStroke(slop));
    }
}
