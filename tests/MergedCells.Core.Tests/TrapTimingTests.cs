using AutoFixture;

namespace MergedCells.Core.Tests;

public class TrapTimingTests
{
    private const double Tolerance = 1e-9;

    // The slide's own speed and cap play no part in the return
    private static readonly Fixture Anonymous = new();

    private static SlideConfig Config(double shrink = 0.2, double pause = 0.1, double grow = 0.2) => new()
    {
        SquaresPerSecond = Anonymous.Create<double>(),
        MaxSeconds = Anonymous.Create<double>(),
        TrapShrinkSeconds = shrink,
        TrapPauseSeconds = pause,
        TrapGrowSeconds = grow,
        TeleportShrinkSeconds = Anonymous.Create<double>(),
        TeleportGrowSeconds = Anonymous.Create<double>(),
    };

    // Shrinking on the trap, hidden, then growing on the start: 0.2, 0.1 and 0.2 seconds
    [Theory]
    [InlineData(0, false, 1)]
    [InlineData(0.1, false, 0.75)]
    [InlineData(0.25, true, 0)]
    [InlineData(0.4, true, 0.75)]
    [InlineData(0.5, true, 1)]
    [InlineData(0.7, true, 1)]
    public void At_gives_the_players_frame(double seconds, bool onStart, double scale)
    {
        var frame = TrapTiming.At(seconds, Config());

        Assert.Equal(onStart, frame.OnStart);
        Assert.Equal(scale, frame.Scale, Tolerance);
    }

    [Fact]
    public void Seconds_is_the_shrink_the_pause_and_the_grow()
    {
        Assert.Equal(0.5, TrapTiming.Seconds(Config()), Tolerance);
    }

    [Fact]
    public void Zero_pause_is_accepted()
    {
        Assert.Equal(0.4, TrapTiming.Seconds(Config(pause: 0)), Tolerance);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Bad_shrink_time_throws(double shrink)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TrapTiming.Seconds(Config(shrink: shrink)));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Bad_pause_throws(double pause)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TrapTiming.Seconds(Config(pause: pause)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Bad_grow_time_throws(double grow)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TrapTiming.Seconds(Config(grow: grow)));
    }

    [Fact]
    public void Frame_with_a_bad_time_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TrapTiming.At(0, Config(grow: 0)));
    }

    [Fact]
    public void No_config_throws()
    {
        Assert.Throws<ArgumentNullException>(() => TrapTiming.Seconds(null!));
    }
}
