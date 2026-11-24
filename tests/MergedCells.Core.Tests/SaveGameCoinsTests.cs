using System.Text.Json;
using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

public class SaveGameCoinsTests
{
    [Theory]
    [AutoData]
    public void Fresh_save_has_no_coins(Guid id)
    {
        var save = new SaveGame();

        Assert.Equal((0L, (long?)null), (save.Coins, save.CustomPayout(id)));
    }

    [Theory]
    [AutoData]
    public void Coins_round_trip(uint amount)
    {
        var save = new SaveGame();
        save.AddCoins(amount);

        Assert.Equal((long)amount, SaveGame.Parse(save.ToJson()).Coins);
    }

    [Theory]
    [AutoData]
    public void Custom_payouts_round_trip(Guid id, uint payout)
    {
        var save = new SaveGame();
        save.SetCustomPayout(id, payout + 1L);

        Assert.Equal(payout + 1L, SaveGame.Parse(save.ToJson()).CustomPayout(id));
    }

    [Theory]
    [AutoData]
    public void Custom_payouts_are_written_in_id_order(Guid first, Guid second, Guid third)
    {
        var save = new SaveGame();
        foreach (var id in new[] { first, second, third })
        {
            save.SetCustomPayout(id, 1);
        }

        using var document = JsonDocument.Parse(save.ToJson());
        var written = document.RootElement.GetProperty("coins").GetProperty("customPayouts").EnumerateObject().Select(p => p.Name);

        Assert.Equal(new[] { first, second, third }.Select(id => id.ToString("D")).Order(StringComparer.Ordinal), written);
    }

    [Theory]
    [AutoData]
    public void Add_coins_adds(uint first, uint second)
    {
        var save = new SaveGame();
        save.AddCoins(first);
        save.AddCoins(second);

        Assert.Equal((long)first + second, save.Coins);
    }

    [Fact]
    public void Add_coins_saturates()
    {
        var save = new SaveGame();
        save.AddCoins(long.MaxValue);
        save.AddCoins(1);

        Assert.Equal(long.MaxValue, save.Coins);
    }

    [Theory]
    [AutoData]
    public void Add_coins_throws_for_a_negative_amount(uint balance)
    {
        var save = new SaveGame();
        save.AddCoins(balance);

        Assert.Throws<ArgumentOutOfRangeException>(() => save.AddCoins(-1));
        Assert.Equal((long)balance, save.Coins);
    }

    [Theory]
    [AutoData]
    public void Set_custom_payout_throws_below_1(Guid id)
    {
        var save = new SaveGame();

        Assert.Throws<ArgumentOutOfRangeException>(() => save.SetCustomPayout(id, 0));
        Assert.Null(save.CustomPayout(id));
    }

    [Fact]
    public void Set_custom_payout_throws_for_an_empty_id()
    {
        Assert.Throws<ArgumentException>(() => new SaveGame().SetCustomPayout(Guid.Empty, 1));
    }
}
