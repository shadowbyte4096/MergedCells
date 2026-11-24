namespace MergedCells.Core.Tests;

public class CoinWordsTests
{
    [Theory]
    [InlineData(0, "0")]
    [InlineData(1240, "1,240")]
    public void Balance_has_thousands_separators(long coins, string expected)
    {
        Assert.Equal(expected, CoinWords.Balance(coins));
    }

    [Theory]
    [InlineData(1, "+1 coin")]
    [InlineData(20, "+20 coins")]
    [InlineData(1000, "+1,000 coins")]
    public void Earned_reads_as_a_gain(long coins, string expected)
    {
        Assert.Equal(expected, CoinWords.Earned(coins));
    }
}
