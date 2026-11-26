using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

public class WalletBuyTests
{
    private static readonly CoinsConfig Config = new() { PerStar = 10, Daily = 20, PerPar = 1, PercentPerScore = 5 };

    private readonly SaveGame save = new();

    private Wallet Wallet => new(save, Config);

    [Theory]
    [AutoData]
    public void Buy_takes_the_price_and_owns_the_item(ushort price, ushort left)
    {
        save.AddCoins(price + left);

        Assert.True(Wallet.Buy("ember", price));
        Assert.Equal(((long)left, true), (save.Coins, save.Owned.Contains("ember")));
    }

    [Theory]
    [AutoData]
    public void Buy_short_changes_nothing(ushort balance)
    {
        save.AddCoins(balance);

        Assert.False(Wallet.Buy("ember", balance + 1L));
        Assert.Equal(((long)balance, false), (save.Coins, save.Owned.Contains("ember")));
    }

    [Theory]
    [AutoData]
    public void Buy_owned_item_changes_nothing(ushort price)
    {
        save.AddCoins(price * 2L);
        Wallet.Buy("ember", price);

        Assert.False(Wallet.Buy("ember", price));
        Assert.Equal((long)price, save.Coins);
    }

    [Theory]
    [AutoData]
    public void Buy_with_exactly_the_price_leaves_zero(ushort price)
    {
        save.AddCoins(price);

        Assert.True(Wallet.Buy("ember", price));
        Assert.Equal(0L, save.Coins);
    }

    [Theory]
    [AutoData]
    public void Buy_with_a_bad_id_throws_before_spending(ushort price)
    {
        save.AddCoins(price);

        Assert.Throws<ArgumentException>(() => Wallet.Buy("Not An Id", price));
        Assert.Equal((long)price, save.Coins);
    }

    [Theory]
    [AutoData]
    public void Buy_does_not_choose_the_item(ushort price)
    {
        save.AddCoins(price);
        Wallet.Buy("ember", price);

        Assert.Null(save.ChosenTheme);
    }
}
