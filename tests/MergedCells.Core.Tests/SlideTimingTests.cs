using AutoFixture;

namespace MergedCells.Core.Tests;

public class SlideTimingTests
{
    private const double Tolerance = 1e-9;

    // The trap's return times play no part in a slide's
    private static readonly Fixture Anonymous = new();

    private static SlideConfig Config(double squaresPerSecond, double maxSeconds) => new()
    {
        SquaresPerSecond = squaresPerSecond,
        MaxSeconds = maxSeconds,
        TrapShrinkSeconds = Anonymous.Create<double>(),
        TrapPauseSeconds = Anonymous.Create<double>(),
        TrapGrowSeconds = Anonymous.Create<double>(),
        TeleportShrinkSeconds = Anonymous.Create<double>(),
        TeleportGrowSeconds = Anonymous.Create<double>(),
    };

    [Theory]
    [InlineData(1, 1.0 / 15)]
    [InlineData(3, 0.2)]
    [InlineData(6, 0.4)]
    [InlineData(100, 0.4)]
    public void Seconds_with_the_shipped_numbers(int squares, double expected)
    {
        Assert.Equal(expected, SlideTiming.Seconds(squares, Config(15, 0.4)), Tolerance);
    }

    [Theory]
    [InlineData(5, 0.5)]
    [InlineData(10, 1.0)]
    [InlineData(20, 1.0)]
    public void Seconds_with_other_numbers(int squares, double expected)
    {
        Assert.Equal(expected, SlideTiming.Seconds(squares, Config(10, 1)), Tolerance);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Squares_below_one_throws(int squares)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SlideTiming.Seconds(squares, Config(15, 0.4)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Bad_speed_throws(double squaresPerSecond)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SlideTiming.Seconds(1, Config(squaresPerSecond, 0.4)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Bad_cap_throws(double maxSeconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SlideTiming.Seconds(1, Config(15, maxSeconds)));
    }

    [Fact]
    public void No_config_throws()
    {
        Assert.Throws<ArgumentNullException>(() => SlideTiming.Seconds(1, null!));
    }
}
