namespace MergedCells.Core.Tests;

public class ShopWordsTests
{
    [Theory]
    [InlineData(0, "You have 0 coins")]
    [InlineData(1, "You have 1 coin")]
    [InlineData(1240, "You have 1,240 coins")]
    public void Balance_says_what_you_have(long coins, string expected)
    {
        Assert.Equal(expected, ShopWords.Balance(coins));
    }

    [Theory]
    [InlineData(1, "You need 1 more coin")]
    [InlineData(160, "You need 160 more coins")]
    [InlineData(1500, "You need 1,500 more coins")]
    public void Short_says_how_many_more(long coins, string expected)
    {
        Assert.Equal(expected, ShopWords.Short(coins));
    }

    [Theory]
    [InlineData(300, "Buy · 300")]
    [InlineData(1500, "Buy · 1,500")]
    public void Buy_label_carries_the_price(int price, string expected)
    {
        Assert.Equal(expected, ShopWords.Buy(price));
    }

    [Fact]
    public void Fixed_words()
    {
        Assert.Equal(("Free", "Owned", "In use", "Use"), (ShopWords.Free, ShopWords.Owned, ShopWords.InUse, ShopWords.Use));
    }
}
