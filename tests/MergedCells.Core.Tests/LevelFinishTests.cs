namespace MergedCells.Core.Tests;

public class LevelFinishTests
{
    [Theory]
    [InlineData(2, null, 2)]
    [InlineData(3, 1, 2)]
    [InlineData(2, 2, 0)]
    [InlineData(1, 3, 0)]
    public void New_stars_are_the_rise_over_the_previous_best(int stars, int? previousStars, int expected)
    {
        Assert.Equal(expected, new LevelFinish(5, stars, previousStars is null ? null : 9, previousStars).NewStars);
    }
}
