using System.Numerics;
using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

public class DragTrackerTests
{
    private const float Slop = 14;

    // Pressed at (100, 100), then moved 11.2 units: still inside the dead zone
    private static DragTracker InsideTheDeadZone(out GestureStep? step)
    {
        var drag = new DragTracker(Slop);
        drag.Press(100, 100);
        step = drag.Move(110, 105);
        return drag;
    }

    [Theory]
    [AutoData]
    public void Move_without_a_press_gives_nothing(float x, float y)
    {
        Assert.Null(new DragTracker(Slop).Move(x, y));
    }

    [Fact]
    public void Movement_inside_the_dead_zone_gives_no_step()
    {
        InsideTheDeadZone(out var step);

        Assert.Null(step);
    }

    [Fact]
    public void First_step_past_the_dead_zone_starts_from_the_previous_position()
    {
        var drag = InsideTheDeadZone(out _);

        Assert.Equal(new GestureStep(1, new Vector2(110, 105), new Vector2(120, 110)), drag.Move(120, 110));
    }

    [Fact]
    public void Later_steps_follow_the_pointer()
    {
        var drag = InsideTheDeadZone(out _);
        drag.Move(120, 110);

        Assert.Equal(new GestureStep(1, new Vector2(120, 110), new Vector2(130, 110)), drag.Move(130, 110));
    }

    [Fact]
    public void Drag_past_the_dead_zone_keeps_giving_steps_back_inside_it()
    {
        var drag = InsideTheDeadZone(out _);
        drag.Move(120, 110);

        Assert.Equal(new GestureStep(1, new Vector2(120, 110), new Vector2(101, 100)), drag.Move(101, 100));
    }

    [Fact]
    public void Release_ends_the_drag()
    {
        var drag = InsideTheDeadZone(out _);
        drag.Move(120, 110);
        drag.Release();

        Assert.Equal((false, (GestureStep?)null), (drag.IsDown, drag.Move(140, 110)));
    }

    [Fact]
    public void Press_restarts_the_dead_zone()
    {
        var drag = InsideTheDeadZone(out _);
        drag.Move(120, 110);
        drag.Press(300, 300);

        Assert.Null(drag.Move(305, 300));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NaN)]
    public void Dead_zone_that_is_negative_or_not_finite_throws(float slop)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DragTracker(slop));
    }
}
