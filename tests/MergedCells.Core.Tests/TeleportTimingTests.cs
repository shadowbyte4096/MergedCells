using AutoFixture;

namespace MergedCells.Core.Tests;

public class TeleportTimingTests
{
    private const double Tolerance = 1e-9;

    // The slide's own speed and the trap's times play no part in the blink
    private static readonly Fixture Anonymous = new();

    private static SlideConfig Config(double shrink = 0.12, double grow = 0.12) => new()
    {
        SquaresPerSecond = Anonymous.Create<double>(),
        MaxSeconds = Anonymous.Create<double>(),
        TrapShrinkSeconds = Anonymous.Create<double>(),
        TrapPauseSeconds = Anonymous.Create<double>(),
        TrapGrowSeconds = Anonymous.Create<double>(),
        TeleportShrinkSeconds = shrink,
        TeleportGrowSeconds = grow,
    };

    // Shrinking into the teleporter for 0.12 s, then growing out of its partner for 0.12 s
    [Theory]
    [InlineData(0, false, 1)]
    [InlineData(0.06, false, 0.75)]
    [InlineData(0.12, true, 0)]
    [InlineData(0.18, true, 0.75)]
    [InlineData(0.24, true, 1)]
    [InlineData(1, true, 1)]
    public void At_without_a_wait_gives_the_players_frame(double seconds, bool onExit, double scale)
    {
        var frame = TeleportTiming.At(seconds, 0, Config());

        Assert.Equal(onExit, frame.OnExit);
        Assert.Equal(scale, frame.Scale, Tolerance);
    }

    // The camera's glide ends 0.5 s after the player reached the teleporter, so the grow starts then
    [Theory]
    [InlineData(0.3, true, 0)]
    [InlineData(0.56, true, 0.75)]
    [InlineData(0.62, true, 1)]
    public void At_with_a_wait_keeps_the_player_hidden_until_it_ends(double seconds, bool onExit, double scale)
    {
        var frame = TeleportTiming.At(seconds, 0.5, Config());

        Assert.Equal(onExit, frame.OnExit);
        Assert.Equal(scale, frame.Scale, Tolerance);
    }

    [Fact]
    public void Seconds_without_a_wait_is_the_shrink_and_the_grow()
    {
        Assert.Equal(0.24, TeleportTiming.Seconds(0, Config()), Tolerance);
    }

    [Fact]
    public void Seconds_with_a_long_wait_is_the_wait_and_the_grow()
    {
        Assert.Equal(0.62, TeleportTiming.Seconds(0.5, Config()), Tolerance);
    }

    [Fact]
    public void Wait_shorter_than_the_shrink_adds_nothing()
    {
        Assert.Equal(0.24, TeleportTiming.Seconds(0.05, Config()), Tolerance);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Bad_shrink_time_throws(double shrink)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TeleportTiming.Seconds(0, Config(shrink: shrink)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Bad_grow_time_throws(double grow)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TeleportTiming.Seconds(0, Config(grow: grow)));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Bad_wait_throws(double wait)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TeleportTiming.Seconds(wait, Config()));
    }

    [Fact]
    public void Frame_with_a_bad_wait_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TeleportTiming.At(0, -1, Config()));
    }

    [Fact]
    public void No_config_throws()
    {
        Assert.Throws<ArgumentNullException>(() => TeleportTiming.Seconds(0, null!));
    }
}
