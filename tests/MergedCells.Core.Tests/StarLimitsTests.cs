namespace MergedCells.Core.Tests;

public class StarLimitsTests
{
    private static Map Shipped(int id) => ShippedCampaignTests.Campaign().Levels.Single(level => level.Id == id).Map;

    // Chapter 1's medium levels (1-star limit 2 past the 2-star one) and hard levels (1 past), with the levelStars chunk 12
    // set from route counts. These literals are the point: the rule must give back what was shipped
    [Theory]
    [InlineData(18, 2, 2, 4)]
    [InlineData(19, 2, 2, 4)]
    [InlineData(20, 2, 2, 4)]
    [InlineData(21, 2, 2, 4)]
    [InlineData(7, 2, 2, 4)]
    [InlineData(22, 1, 2, 3)]
    [InlineData(23, 1, 2, 3)]
    [InlineData(24, 1, 1, 2)]
    [InlineData(25, 1, 2, 3)]
    [InlineData(8, 1, 1, 2)]
    public void Gives_chapter_1_s_shipped_limits(int id, int oneStarExtraMoves, int twoStarMin, int oneStarMin)
    {
        var expected = new StarsConfig
        {
            TwoStarMinExtraMoves = twoStarMin,
            TwoStarExtraParPercent = 0,
            OneStarMinExtraMoves = oneStarMin,
            OneStarExtraParPercent = 0,
        };

        Assert.Equal(expected, StarLimits.Reachable(Shipped(id), 10, 10, oneStarExtraMoves));
    }

    // Slide is one strip: every route past par goes back and forth, and there are too few of them
    [Fact]
    public void Null_when_routes_never_reach_the_minimum()
    {
        Assert.Null(StarLimits.Reachable(Shipped(11), 10, 10, 2));
    }

    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(10, 0, 0)]
    [InlineData(10, 21, 0)]
    [InlineData(10, 10, -1)]
    public void Rejects_numbers_out_of_range(int minRoutes, int maxExtraMoves, int oneStarExtraMoves)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => StarLimits.Reachable(TestMaps.Example(), minRoutes, maxExtraMoves, oneStarExtraMoves));
    }

    [Fact]
    public void Unsolvable_map_throws()
    {
        var ring = Map.Create(TestMaps.Grid("AAA", "ABA", "AAA"), new(0, 0), new(1, 1));

        Assert.Throws<ArgumentException>(() => StarLimits.Reachable(ring, 10, 10, 2));
    }
}
