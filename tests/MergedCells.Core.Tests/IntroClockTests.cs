using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

public class IntroClockTests
{
    private static IntroClock Started()
    {
        var clock = new IntroClock();
        clock.Tick(1);
        return clock;
    }

    [Theory]
    [AutoData]
    public void The_first_tick_adds_nothing(double delta)
    {
        var clock = new IntroClock();

        Assert.Equal(0, clock.Tick(delta));
    }

    [Theory]
    [InlineData(0.008)]
    [InlineData(0.016)]
    [InlineData(0.05)]
    public void A_tick_adds_its_delta(double delta)
    {
        Assert.Equal(delta, Started().Tick(delta), 12);
    }

    [Theory]
    [InlineData(0.2)]
    [InlineData(5)]
    [InlineData(60)]
    public void A_long_tick_adds_only_the_max_step(double delta)
    {
        Assert.Equal(IntroClock.MaxStep, Started().Tick(delta), 12);
    }

    [Fact]
    public void Ticks_add_up()
    {
        var clock = Started();
        clock.Tick(0.016);
        clock.Tick(0.017);

        Assert.Equal(0.033, clock.Elapsed, 12);
    }
}
