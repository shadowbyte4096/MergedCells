using System.Text.Json;
using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

public class SaveGameCosmeticsTests
{
    [Fact]
    public void Fresh_save_owns_nothing_and_wears_the_defaults()
    {
        var save = new SaveGame();

        Assert.Equal((0, (string?)null, (string?)null), (save.Owned.Count, save.ChosenTheme, save.ChosenLook));
    }

    [Fact]
    public void Round_trip_keeps_owned_and_choices()
    {
        var save = new SaveGame();
        save.AddOwned("prism");
        save.AddOwned("diamond");
        save.ChosenTheme = "prism";
        save.ChosenLook = "diamond";

        var read = SaveGame.Parse(save.ToJson());

        Assert.Equal(("diamond prism", "prism", "diamond"), (string.Join(" ", read.Owned.Order(StringComparer.Ordinal)), read.ChosenTheme, read.ChosenLook));
    }

    [Fact]
    public void Owned_are_written_in_ordinal_order()
    {
        var save = new SaveGame();
        foreach (var id in new[] { "tide", "block", "ember", "ring" })
        {
            save.AddOwned(id);
        }

        using var document = JsonDocument.Parse(save.ToJson());
        var written = document.RootElement.GetProperty("cosmetics").GetProperty("owned").EnumerateArray().Select(e => e.GetString());

        Assert.Equal(["block", "ember", "ring", "tide"], written);
    }

    [Fact]
    public void Add_owned_twice_says_so()
    {
        var save = new SaveGame();
        save.AddOwned("ember");

        Assert.False(save.AddOwned("ember"));
    }

    [Theory]
    [InlineData("Ember")]
    [InlineData("a--b")]
    [InlineData("")]
    public void Add_owned_rejects_a_bad_id(string id)
    {
        Assert.Throws<ArgumentException>(() => new SaveGame().AddOwned(id));
    }

    [Fact]
    public void Choosing_an_unowned_theme_throws()
    {
        var save = new SaveGame();

        Assert.Throws<ArgumentException>(() => save.ChosenTheme = "ember");
        Assert.Null(save.ChosenTheme);
    }

    [Fact]
    public void Choosing_an_unowned_look_throws()
    {
        Assert.Throws<ArgumentException>(() => new SaveGame().ChosenLook = "ring");
    }

    [Fact]
    public void Choosing_null_wears_the_default()
    {
        var save = new SaveGame();
        save.AddOwned("ember");
        save.ChosenTheme = "ember";

        save.ChosenTheme = null;

        Assert.Null(save.ChosenTheme);
    }

    [Theory]
    [AutoData]
    public void Spend_takes_the_amount(uint spend, uint left)
    {
        var save = new SaveGame();
        save.AddCoins((long)spend + left);

        Assert.True(save.SpendCoins(spend));
        Assert.Equal((long)left, save.Coins);
    }

    [Theory]
    [AutoData]
    public void Spend_more_than_the_balance_changes_nothing(uint balance)
    {
        var save = new SaveGame();
        save.AddCoins(balance);

        Assert.False(save.SpendCoins(balance + 1L));
        Assert.Equal((long)balance, save.Coins);
    }

    [Fact]
    public void Spend_negative_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SaveGame().SpendCoins(-1));
    }
}
