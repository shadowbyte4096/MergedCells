using System.Numerics;
using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

public class PinchTrackerTests
{
    private const float Slop = 14;

    private static PinchTracker Pinching(int a, int b, float slop = 0)
    {
        var pinch = new PinchTracker(slop);
        pinch.Press(a, 100, 100);
        pinch.Press(b, 200, 100);
        return pinch;
    }

    [Theory]
    [AutoData]
    public void One_finger_gives_no_step(int finger, float x, float y)
    {
        var pinch = new PinchTracker(0);
        pinch.Press(finger, x, y);
        pinch.Drag(finger, x + 50, y);

        Assert.Equal((false, (GestureStep?)null), (pinch.IsPinching, pinch.TakeStep()));
    }

    [Theory]
    [AutoData]
    public void Spreading_two_fingers_scales_up_about_their_midpoint(int a)
    {
        var pinch = Pinching(a, a + 1);
        pinch.Drag(a + 1, 300, 100);

        Assert.Equal(new GestureStep(2, new Vector2(150, 100), new Vector2(200, 100)), pinch.TakeStep());
    }

    [Theory]
    [AutoData]
    public void Pinching_two_fingers_together_scales_down(int a)
    {
        var pinch = Pinching(a, a + 1);
        pinch.Drag(a + 1, 150, 100);

        Assert.Equal(new GestureStep(0.5f, new Vector2(150, 100), new Vector2(125, 100)), pinch.TakeStep());
    }

    [Theory]
    [AutoData]
    public void Second_finger_down_starts_a_pinch(int a, float x, float y)
    {
        var pinch = new PinchTracker(0);
        pinch.Press(a, x, y);
        var afterOne = pinch.IsPinching;
        pinch.Press(a + 1, x + 10, y);

        Assert.Equal((false, true), (afterOne, pinch.IsPinching));
    }

    [Theory]
    [AutoData]
    public void Third_finger_is_ignored(int a)
    {
        var pinch = Pinching(a, a + 1);
        pinch.Press(a + 2, 400, 400);
        pinch.Drag(a + 2, 500, 500);
        var afterThird = pinch.TakeStep();
        pinch.Drag(a + 1, 300, 100);

        Assert.Equal(
            ((GestureStep?)null, new GestureStep(2, new Vector2(150, 100), new Vector2(200, 100))),
            (afterThird, pinch.TakeStep()));
    }

    [Theory]
    [InlineAutoData(0)]
    [InlineAutoData(1)]
    public void Lifting_either_pinching_finger_ends_the_pinch(int lifted, int a)
    {
        var pinch = Pinching(a, a + 1);
        pinch.Release(a + lifted);
        pinch.Drag(a + 1 - lifted, 400, 400);

        Assert.Equal((false, (GestureStep?)null), (pinch.IsPinching, pinch.TakeStep()));
    }

    [Theory]
    [AutoData]
    public void Repeated_press_counts_as_a_lost_release(int a)
    {
        var pinch = Pinching(a, a + 1);
        pinch.Press(a, 0, 100);
        pinch.Drag(a + 1, 400, 100);

        // A is now 200 from B, so moving B 200 further doubles the distance
        Assert.Equal(
            (true, new GestureStep(2, new Vector2(100, 100), new Vector2(200, 100))),
            (pinch.IsPinching, pinch.TakeStep()));
    }

    [Theory]
    [AutoData]
    public void Reset_forgets_every_finger(int a)
    {
        var pinch = Pinching(a, a + 1);
        pinch.Reset();
        pinch.Drag(a, 50, 50);
        pinch.Drag(a + 1, 300, 100);

        Assert.Equal((false, (GestureStep?)null), (pinch.IsPinching, pinch.TakeStep()));
    }

    // Starting on one point leaves no previous distance; ending on one point makes the new distance zero
    [Theory]
    [InlineAutoData(100, 200)]
    [InlineAutoData(200, 100)]
    public void Fingers_on_one_point_give_no_step(float pressX, float dragX, int a)
    {
        var pinch = new PinchTracker(0);
        pinch.Press(a, 100, 100);
        pinch.Press(a + 1, pressX, 100);
        pinch.Drag(a + 1, dragX, 100);

        Assert.Null(pinch.TakeStep());
    }

    [Theory]
    [AutoData]
    public void No_movement_gives_no_step(int a)
    {
        var pinch = Pinching(a, a + 1);
        pinch.Drag(a + 1, 300, 100);
        pinch.TakeStep();

        Assert.Null(pinch.TakeStep());
    }

    // The first row moves the midpoint 5 and the distance 10; the second moves the midpoint 10 and the distance 2
    [Theory]
    [InlineAutoData(210, 100)]
    [InlineAutoData(200, 120)]
    public void Movement_inside_the_dead_zone_gives_no_step(float x, float y, int a)
    {
        var pinch = Pinching(a, a + 1, Slop);
        pinch.Drag(a + 1, x, y);

        Assert.Null(pinch.TakeStep());
    }

    [Theory]
    [AutoData]
    public void First_step_past_the_dead_zone_starts_from_the_previous_frame(int a)
    {
        var pinch = Pinching(a, a + 1, Slop);
        pinch.Drag(a + 1, 210, 100);
        var inside = pinch.TakeStep();
        pinch.Drag(a + 1, 230, 100);

        Assert.Equal(
            ((GestureStep?)null, new GestureStep(130f / 110, new Vector2(155, 100), new Vector2(165, 100))),
            (inside, pinch.TakeStep()));
    }

    // Both fingers move each frame, as a phone reports them: the midpoint crosses the dead zone in the second frame,
    // and the distance never changes, so the step pans without zooming
    [Theory]
    [AutoData]
    public void Moving_both_fingers_past_the_dead_zone_pans(int a)
    {
        var pinch = Pinching(a, a + 1, Slop);
        pinch.Drag(a, 100, 110);
        pinch.Drag(a + 1, 200, 110);
        var inside = pinch.TakeStep();
        pinch.Drag(a, 100, 120);
        pinch.Drag(a + 1, 200, 120);

        Assert.Equal(
            ((GestureStep?)null, new GestureStep(1, new Vector2(150, 110), new Vector2(150, 120))),
            (inside, pinch.TakeStep()));
    }

    [Theory]
    [AutoData]
    public void Dead_zone_restarts_with_each_pinch(int a)
    {
        var pinch = Pinching(a, a + 1, Slop);
        pinch.Drag(a + 1, 300, 100);
        pinch.TakeStep();
        pinch.Release(a + 1);
        pinch.Press(a + 1, 200, 100);
        pinch.Drag(a + 1, 210, 100);

        Assert.Null(pinch.TakeStep());
    }

    // The distance is zero for a frame, so that frame and the next give nothing, and the one after carries on
    [Theory]
    [AutoData]
    public void Pinch_carries_on_after_a_frame_with_both_fingers_on_one_point(int a)
    {
        var pinch = Pinching(a, a + 1);
        pinch.Drag(a + 1, 100, 100);
        var together = pinch.TakeStep();
        pinch.Drag(a + 1, 150, 100);
        var fromTogether = pinch.TakeStep();
        pinch.Drag(a + 1, 200, 100);

        Assert.Equal(
            ((GestureStep?)null, (GestureStep?)null, new GestureStep(2, new Vector2(125, 100), new Vector2(150, 100))),
            (together, fromTogether, pinch.TakeStep()));
    }

    [Theory]
    [AutoData]
    public void Pinch_past_the_dead_zone_keeps_giving_steps_back_inside_it(int a)
    {
        var pinch = Pinching(a, a + 1, Slop);
        pinch.Drag(a + 1, 230, 100);
        pinch.TakeStep();
        pinch.Drag(a + 1, 205, 100);

        Assert.Equal(new GestureStep(105f / 130, new Vector2(165, 100), new Vector2(152.5f, 100)), pinch.TakeStep());
    }

    [Theory]
    [AutoData]
    public void Third_finger_held_through_a_lift_joins_only_when_pressed_again(int a)
    {
        var pinch = Pinching(a, a + 1);
        pinch.Press(a + 2, 300, 100);
        pinch.Release(a + 1);
        pinch.Drag(a + 2, 320, 100);
        var afterLift = pinch.IsPinching;
        pinch.Press(a + 2, 320, 100);

        Assert.Equal((false, true), (afterLift, pinch.IsPinching));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NaN)]
    public void Dead_zone_that_is_negative_or_not_finite_throws(float slop)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PinchTracker(slop));
    }

    // A frame can hold several mouse motions, each its own drag step
    [Theory]
    [AutoData]
    public void Then_joins_two_steps_into_one(float firstScale, float secondScale, Vector2 from, Vector2 through, Vector2 to)
    {
        var joined = new GestureStep(firstScale, from, through).Then(new GestureStep(secondScale, through, to));

        Assert.Equal(new GestureStep(firstScale * secondScale, from, to), joined);
    }
}
