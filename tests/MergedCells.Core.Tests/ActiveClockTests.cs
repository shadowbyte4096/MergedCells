using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

public class ActiveClockTests
{
    // Frame times in seconds, kept small and positive so their sum is easy to check
    private static double Frame(byte value) => (value + 1) / 1000.0;

    [Theory]
    [AutoData]
    public void Frames_after_the_first_are_counted(byte first, byte second, byte third)
    {
        var clock = new ActiveClock();
        clock.Tick(Frame(first));

        clock.Tick(Frame(second));
        clock.Tick(Frame(third));

        Assert.Equal((long)((Frame(second) + Frame(third)) * 1000), clock.Milliseconds);
    }

    // The frame after a map appears includes making it, which isn't play
    [Theory]
    [AutoData]
    public void First_frame_after_a_reset_is_dropped(byte counted, byte making)
    {
        var clock = new ActiveClock();
        clock.Tick(Frame(counted));
        clock.Tick(Frame(counted));

        clock.Reset();
        clock.Tick(making * 10.0);

        Assert.Equal(0, clock.Milliseconds);
    }

    [Theory]
    [AutoData]
    public void Frames_while_paused_are_not_counted(byte before, byte paused)
    {
        var clock = new ActiveClock();
        clock.Tick(Frame(before));
        clock.Tick(Frame(before));

        clock.Pause();
        clock.Tick(Frame(paused));

        Assert.Equal((long)(Frame(before) * 1000), clock.Milliseconds);
    }

    // Godot's first frame after the app comes back spans the whole time it was away
    [Theory]
    [AutoData]
    public void First_frame_after_resuming_is_dropped(byte before, byte away, byte after)
    {
        var clock = new ActiveClock();
        clock.Tick(Frame(before));
        clock.Tick(Frame(before));
        clock.Pause();

        clock.Resume();
        clock.Tick(away * 60.0);
        clock.Tick(Frame(after));

        Assert.Equal((long)((Frame(before) + Frame(after)) * 1000), clock.Milliseconds);
    }

    [Fact]
    public void Resuming_a_running_clock_keeps_counting_its_next_frame()
    {
        var clock = new ActiveClock();
        clock.Tick(0.016);

        clock.Resume();
        clock.Tick(0.020);

        Assert.Equal(20, clock.Milliseconds);
    }
}
