using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

// AutoFixture's floats are whole numbers, so offsets built from the threshold land exactly on it
public class SwipeDetectorTests
{
    [Theory]
    [InlineAutoData(1, 0, Direction.Right)]
    [InlineAutoData(-1, 0, Direction.Left)]
    [InlineAutoData(0, 1, Direction.Down)]
    [InlineAutoData(0, -1, Direction.Up)]
    public void Reaching_the_threshold_fires_its_direction(
        int signX, int signY, Direction expected, float threshold, int finger, float x, float y)
    {
        var swipes = new SwipeDetector(threshold);
        swipes.Press(finger, x, y);

        var direction = swipes.Drag(finger, x + signX * threshold, y + signY * threshold);

        Assert.Equal(expected, direction);
    }

    [Theory]
    [AutoData]
    public void Drag_just_under_the_threshold_returns_null(float threshold, int finger, float x, float y)
    {
        var swipes = new SwipeDetector(threshold);
        swipes.Press(finger, x, y);

        Assert.Null(swipes.Drag(finger, x + threshold - 1, y));
    }

    [Theory]
    [InlineAutoData(1, 1, 1, 0, Direction.Right)]
    [InlineAutoData(-1, 1, 1, 0, Direction.Left)]
    [InlineAutoData(1, 0, 1, 1, Direction.Down)]
    [InlineAutoData(1, 0, -1, 1, Direction.Up)]
    public void Larger_axis_wins_on_a_diagonal(
        int signX, int extraX, int signY, int extraY, Direction expected, float threshold, int finger, float x, float y)
    {
        var swipes = new SwipeDetector(threshold);
        swipes.Press(finger, x, y);

        var direction = swipes.Drag(finger, x + signX * (threshold + extraX), y + signY * (threshold + extraY));

        Assert.Equal(expected, direction);
    }

    [Theory]
    [AutoData]
    public void Exact_diagonal_waits_for_a_later_drag(float threshold, int finger, float x, float y)
    {
        var swipes = new SwipeDetector(threshold);
        swipes.Press(finger, x, y);

        var tie = swipes.Drag(finger, x + threshold, y + threshold);
        var broken = swipes.Drag(finger, x + threshold + 1, y + threshold);

        Assert.Equal((null, Direction.Right), (tie, broken));
    }

    [Theory]
    [AutoData]
    public void Distance_is_measured_from_the_touch_down_point(float threshold, int finger, float x, float y)
    {
        var swipes = new SwipeDetector(threshold);
        swipes.Press(finger, x, y);

        var first = swipes.Drag(finger, x + threshold - 1, y);
        var second = swipes.Drag(finger, x + threshold, y);

        Assert.Equal((null, Direction.Right), (first, second));
    }

    [Theory]
    [AutoData]
    public void Only_one_move_per_touch(float threshold, int finger, float x, float y)
    {
        var swipes = new SwipeDetector(threshold);
        swipes.Press(finger, x, y);

        var first = swipes.Drag(finger, x + threshold, y);
        var back = swipes.Drag(finger, x - 2 * threshold, y);
        var down = swipes.Drag(finger, x, y + 3 * threshold);

        Assert.Equal((Direction.Right, null, null), (first, back, down));
    }

    [Theory]
    [AutoData]
    public void New_touch_after_release_fires_from_its_own_point(
        float threshold, int finger, float x, float y, float x2, float y2)
    {
        var swipes = new SwipeDetector(threshold);
        swipes.Press(finger, x, y);
        swipes.Drag(finger, x + threshold, y);
        swipes.Release(finger);
        swipes.Press(finger, x2, y2);

        Assert.Equal(Direction.Down, swipes.Drag(finger, x2, y2 + threshold));
    }

    [Theory]
    [AutoData]
    public void Short_drag_then_release_leaves_the_next_touch_fresh(
        float threshold, int finger, float x, float y, float x2, float y2)
    {
        var swipes = new SwipeDetector(threshold);
        swipes.Press(finger, x, y);
        swipes.Drag(finger, x + threshold - 1, y);
        swipes.Release(finger);
        swipes.Press(finger, x2, y2);

        Assert.Equal(Direction.Left, swipes.Drag(finger, x2 - threshold, y2));
    }

    [Theory]
    [AutoData]
    public void Two_fingers_down_block_both(float threshold, int first, int second, float x, float y)
    {
        var swipes = new SwipeDetector(threshold);
        swipes.Press(first, x, y);
        swipes.Press(second, x, y);

        var firstDrag = swipes.Drag(first, x + threshold, y);
        var secondDrag = swipes.Drag(second, x + threshold, y);

        Assert.Equal((null, null), (firstDrag, secondDrag));
    }

    [Theory]
    [AutoData]
    public void Finger_left_after_two_finger_gesture_stays_blocked(
        float threshold, int first, int second, float x, float y)
    {
        var swipes = new SwipeDetector(threshold);
        swipes.Press(first, x, y);
        swipes.Press(second, x, y);
        swipes.Release(second);

        Assert.Null(swipes.Drag(first, x + threshold, y));
    }

    [Theory]
    [AutoData]
    public void Reused_index_while_another_finger_is_down_stays_blocked(
        float threshold, int first, int second, float x, float y, float x2, float y2)
    {
        var swipes = new SwipeDetector(threshold);
        swipes.Press(first, x, y);
        swipes.Press(second, x, y);
        swipes.Release(first);
        swipes.Press(first, x2, y2);

        Assert.Null(swipes.Drag(first, x2 + threshold, y2));
    }

    [Theory]
    [AutoData]
    public void Single_touch_after_all_fingers_lift_fires(
        float threshold, int first, int second, int third, float x, float y)
    {
        var swipes = new SwipeDetector(threshold);
        swipes.Press(first, x, y);
        swipes.Press(second, x, y);
        swipes.Release(first);
        swipes.Release(second);
        swipes.Press(third, x, y);

        Assert.Equal(Direction.Right, swipes.Drag(third, x + threshold, y));
    }

    [Theory]
    [AutoData]
    public void Swipe_before_second_finger_stands_and_nothing_follows(
        float threshold, int first, int second, float x, float y)
    {
        var swipes = new SwipeDetector(threshold);
        swipes.Press(first, x, y);
        var swipe = swipes.Drag(first, x + threshold, y);
        swipes.Press(second, x, y);

        var secondDrag = swipes.Drag(second, x + threshold, y);
        var firstAgain = swipes.Drag(first, x + 3 * threshold, y);

        Assert.Equal((Direction.Right, null, null), (swipe, secondDrag, firstAgain));
    }

    [Theory]
    [AutoData]
    public void Drag_from_unknown_finger_returns_null(float threshold, int known, int unknown, float x, float y)
    {
        var swipes = new SwipeDetector(threshold);
        swipes.Press(known, x, y);

        Assert.Null(swipes.Drag(unknown, x + threshold, y));
    }

    [Theory]
    [AutoData]
    public void Release_of_unknown_finger_does_not_disturb_the_gesture(
        float threshold, int known, int unknown, float x, float y)
    {
        var swipes = new SwipeDetector(threshold);
        swipes.Press(known, x, y);
        swipes.Release(unknown);

        Assert.Equal(Direction.Right, swipes.Drag(known, x + threshold, y));
    }

    [Theory]
    [AutoData]
    public void Press_of_a_finger_already_down_restarts_it(
        float threshold, int finger, float x, float y, float x2, float y2)
    {
        var swipes = new SwipeDetector(threshold);
        swipes.Press(finger, x, y);
        swipes.Drag(finger, x + threshold, y);
        swipes.Press(finger, x2, y2);

        Assert.Equal(Direction.Up, swipes.Drag(finger, x2, y2 - threshold));
    }

    [Theory]
    [AutoData]
    public void Press_of_a_finger_already_down_stays_blocked_with_another_finger_down(
        float threshold, int first, int second, float x, float y, float x2, float y2)
    {
        var swipes = new SwipeDetector(threshold);
        swipes.Press(first, x, y);
        swipes.Press(second, x, y);
        swipes.Press(first, x2, y2);

        Assert.Null(swipes.Drag(first, x2 + threshold, y2));
    }

    [Theory]
    [AutoData]
    public void Reset_forgets_fingers_whose_release_was_lost(
        float threshold, int lost, int next, float x, float y)
    {
        var swipes = new SwipeDetector(threshold);
        swipes.Press(lost, x, y);
        swipes.Reset();
        swipes.Press(next, x, y);

        Assert.Equal(Direction.Right, swipes.Drag(next, x + threshold, y));
    }

    [Theory]
    [AutoData]
    public void Drag_from_a_finger_pressed_before_Reset_returns_null(float threshold, int finger, float x, float y)
    {
        var swipes = new SwipeDetector(threshold);
        swipes.Press(finger, x, y);
        swipes.Reset();

        Assert.Null(swipes.Drag(finger, x + threshold, y));
    }

    // A short flick: the last drag was inside the threshold and the lift is past it
    [Theory]
    [AutoData]
    public void Lift_past_the_threshold_fires_when_no_drag_reached_it(float threshold, int finger, float x, float y)
    {
        var swipes = new SwipeDetector(threshold);
        swipes.Press(finger, x, y);
        swipes.Drag(finger, x + threshold - 1, y);

        Assert.Equal(Direction.Right, swipes.Release(finger, x + threshold, y));
    }

    [Theory]
    [AutoData]
    public void Lift_under_the_threshold_gives_no_move(float threshold, int finger, float x, float y)
    {
        var swipes = new SwipeDetector(threshold);
        swipes.Press(finger, x, y);

        Assert.Null(swipes.Release(finger, x + threshold - 1, y));
    }

    [Theory]
    [AutoData]
    public void Lift_after_the_swipe_fired_gives_no_second_move(float threshold, int finger, float x, float y)
    {
        var swipes = new SwipeDetector(threshold);
        swipes.Press(finger, x, y);
        swipes.Drag(finger, x + threshold, y);

        Assert.Null(swipes.Release(finger, x + threshold + 1, y));
    }

    [Theory]
    [AutoData]
    public void Lift_at_a_position_ends_the_gesture(float threshold, int finger, float x, float y)
    {
        var swipes = new SwipeDetector(threshold);
        swipes.Press(finger, x, y);
        swipes.Release(finger, x + threshold, y);
        swipes.Press(finger, x, y);

        Assert.Equal(Direction.Left, swipes.Drag(finger, x - threshold, y));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void Constructor_rejects_bad_thresholds(float threshold)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SwipeDetector(threshold));
    }
}
