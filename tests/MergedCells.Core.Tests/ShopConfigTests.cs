namespace MergedCells.Core.Tests;

public class ShopConfigTests
{
    private static ShopConfig Prices(params (string Id, int Price)[] prices) =>
        new() { Prices = prices.ToDictionary(price => price.Id, price => price.Price) };

    [Theory]
    [InlineData(0)]
    [InlineData(100_001)]
    public void Price_out_of_range_names_its_path(int price)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => Prices(("ember", price)).CheckRanges());

        Assert.Equal("shop.prices.ember", exception.ParamName);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100_000)]
    public void Price_at_the_ends_of_the_range_passes(int price)
    {
        Prices(("ember", price)).CheckRanges();
    }

    [Fact]
    public void PriceOf_an_unpriced_item_is_null()
    {
        Assert.Null(Prices(("ember", 300)).PriceOf("tide"));
    }
}
