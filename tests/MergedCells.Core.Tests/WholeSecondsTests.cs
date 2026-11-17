namespace MergedCells.Core.Tests;

public class WholeSecondsTests
{
    [Fact]
    public void Fraction_of_a_second_is_dropped()
    {
        var time = new DateTimeOffset(2026, 11, 17, 12, 0, 5, TimeSpan.Zero).AddTicks(9_999_999);

        Assert.Equal(new DateTimeOffset(2026, 11, 17, 12, 0, 5, TimeSpan.Zero), WholeSeconds.Of(time));
    }

    // The level files write UTC, so a local time comes back as the same moment in UTC
    [Fact]
    public void Local_time_becomes_utc()
    {
        var local = new DateTimeOffset(2026, 11, 17, 13, 0, 5, TimeSpan.FromHours(1));

        Assert.Equal(TimeSpan.Zero, WholeSeconds.Of(local).Offset);
    }
}
