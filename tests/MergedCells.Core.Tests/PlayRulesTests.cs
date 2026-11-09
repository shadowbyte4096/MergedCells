namespace MergedCells.Core.Tests;

public class PlayRulesTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void A_negative_limit_throws(int limit)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlayRules(limit));
    }

    [Fact]
    public void Unlimited_has_no_limit()
    {
        Assert.Null(PlayRules.Unlimited.UndoLimit);
    }
}
