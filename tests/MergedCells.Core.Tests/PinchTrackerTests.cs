using System.Numerics;
using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

public class PinchTrackerTests
{
    private static PinchTracker Pinching(int a, int b)
    {
        var pinch = new PinchTracker();
        pinch.Press(a, 100, 100);
        pinch.Press(b, 200, 100);
        return pinch;
    }

    [Theory]
    [AutoData]
    public void One_finger_gives_no_step(int finger, float x, float y)
    {
        var pinch = new PinchTracker();
        pinch.Press(finger, x, y);

        Assert.Equal((false, (PinchStep?)null), (pinch.IsPinching, pinch.Drag(finger, x + 50, y)));
    }

    [Theory]
    [AutoData]
    public void Spreading_two_fingers_scales_up_about_their_midpoint(int a)
    {
        var pinch = Pinching(a, a + 1);

        Assert.Equal(new PinchStep(2, new Vector2(200, 100)), pinch.Drag(a + 1, 300, 100));
    }

    [Theory]
    [AutoData]
    public void Pinching_two_fingers_together_scales_down(int a)
    {
        var pinch = Pinching(a, a + 1);

        Assert.Equal(new PinchStep(0.5f, new Vector2(125, 100)), pinch.Drag(a + 1, 150, 100));
    }

    [Theory]
    [AutoData]
    public void Second_finger_down_starts_a_pinch(int a, float x, float y)
    {
        var pinch = new PinchTracker();
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

        Assert.Equal(
            ((PinchStep?)null, new PinchStep(2, new Vector2(200, 100))),
            (pinch.Drag(a + 2, 500, 500), pinch.Drag(a + 1, 300, 100)));
    }

    [Theory]
    [InlineAutoData(0)]
    [InlineAutoData(1)]
    public void Lifting_either_pinching_finger_ends_the_pinch(int lifted, int a)
    {
        var pinch = Pinching(a, a + 1);
        pinch.Release(a + lifted);
        var other = a + 1 - lifted;

        Assert.Equal((false, (PinchStep?)null), (pinch.IsPinching, pinch.Drag(other, 400, 400)));
    }

    [Theory]
    [AutoData]
    public void Repeated_press_counts_as_a_lost_release(int a)
    {
        var pinch = Pinching(a, a + 1);
        pinch.Press(a, 0, 100);

        // A is now 200 from B, so moving B 200 further doubles the distance
        Assert.Equal((true, new PinchStep(2, new Vector2(200, 100))), (pinch.IsPinching, pinch.Drag(a + 1, 400, 100)));
    }

    [Theory]
    [AutoData]
    public void Reset_forgets_every_finger(int a)
    {
        var pinch = Pinching(a, a + 1);
        pinch.Reset();

        Assert.Equal(
            (false, (PinchStep?)null, (PinchStep?)null),
            (pinch.IsPinching, pinch.Drag(a, 50, 50), pinch.Drag(a + 1, 300, 100)));
    }

    // Starting on one point leaves no previous distance; ending on one point makes the new distance zero
    [Theory]
    [InlineAutoData(100, 200)]
    [InlineAutoData(200, 100)]
    public void Fingers_on_one_point_give_no_step(float pressX, float dragX, int a)
    {
        var pinch = new PinchTracker();
        pinch.Press(a, 100, 100);
        pinch.Press(a + 1, pressX, 100);

        Assert.Null(pinch.Drag(a + 1, dragX, 100));
    }
}
