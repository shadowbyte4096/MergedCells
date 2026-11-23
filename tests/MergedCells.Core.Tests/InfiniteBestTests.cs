using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

public class InfiniteBestTests
{
    [Theory]
    [AutoData]
    public void Max_takes_each_fields_larger(ushort streakA, ushort streakB, uint runA, uint runB, ushort efficiencyA, ushort efficiencyB)
    {
        var a = new InfiniteBest(streakA, runA, efficiencyA);
        var b = new InfiniteBest(streakB, runB, efficiencyB);

        Assert.Equal(new InfiniteBest(Math.Max(streakA, streakB), Math.Max(runA, runB), Math.Max(efficiencyA, efficiencyB)), a.Max(b));
    }

    [Theory]
    [AutoData]
    public void Null_efficiency_is_below_any_number(ushort efficiency)
    {
        Assert.Equal(efficiency, new InfiniteBest(0, 0, null).Max(new InfiniteBest(0, 0, efficiency)).Efficiency);
    }

    [Theory]
    [AutoData]
    public void Number_is_kept_over_a_null(ushort efficiency)
    {
        Assert.Equal(efficiency, new InfiniteBest(0, 0, efficiency).Max(new InfiniteBest(0, 0, null)).Efficiency);
    }

    [Fact]
    public void Two_nulls_stay_null()
    {
        Assert.Null(InfiniteBest.None.Max(InfiniteBest.None).Efficiency);
    }

    [Fact]
    public void None_is_zeros_with_no_efficiency()
    {
        Assert.Equal(new InfiniteBest(0, 0, null), InfiniteBest.None);
    }
}
