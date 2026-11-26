namespace MergedCells.Core.Tests;

public class ShopOffersTests
{
    private static readonly Cosmetics Catalogue = Cosmetics.Parse(Text());

    private static readonly ShopConfig Shop = new()
    {
        Prices = new Dictionary<string, int> { ["ember"] = 300, ["tide"] = 300, ["ring"] = 150 },
    };

    private readonly SaveGame save = new();

    private static string Text()
    {
        const string palette =
            """{"background":"#e3f1ed","ink":"#235e56","card":"#ffffff","muted":"#3a7068","chart":"#3a7068","placeholder":"#e5eceb","disabled":"#8dafa9","pressed":"#497b74","new":"#349f8b","fills":["#f2f5f5","#e0e8e7","#cfdcda","#bdcfcc","#abc2bf"],"outline":"#235e56","end":"#235e56","wall":"#235e56","wallMiddle":"#a9c5c0","trap":"#235e56","teleporter":"#235e56","player":"#235e56"}""";
        static string Theme(string id, string name) => $$"""{"id":"{{id}}","name":"{{name}}","light":{{palette}},"dark":{{palette}}}""";
        static string Look(string id, string name) => $$"""{"id":"{{id}}","name":"{{name}}","side":0.56,"stroke":0.1,"corner":0.1,"filled":false,"turn":0}""";
        return $$"""{"themes":[{{Theme("teal", "Teal")}},{{Theme("ember", "Ember")}},{{Theme("tide", "Tide")}}],"looks":[{{Look("square", "Square")}},{{Look("ring", "Ring")}}]}""";
    }

    private IReadOnlyList<ShopOffer> Themes => ShopOffers.Themes(Catalogue, Shop, save);

    private void Own(string id, bool choose = false)
    {
        save.AddOwned(id);
        if (choose)
        {
            save.ChosenTheme = id;
        }
    }

    [Fact]
    public void Offers_follow_the_catalogue_order()
    {
        Assert.Equal(["teal", "ember", "tide"], Themes.Select(offer => offer.Id));
    }

    [Fact]
    public void Offers_carry_the_names()
    {
        Assert.Equal(["Teal", "Ember", "Tide"], Themes.Select(offer => offer.Name));
    }

    [Fact]
    public void Free_default_with_no_choice_is_in_use()
    {
        Assert.Equal(new ShopOffer("teal", "Teal", null, OfferState.InUse), Themes[0]);
    }

    [Fact]
    public void Free_default_not_chosen_is_free()
    {
        Own("ember", choose: true);

        Assert.Equal(OfferState.Free, Themes[0].State);
    }

    [Fact]
    public void Unowned_priced_item_is_for_sale_at_its_price()
    {
        Assert.Equal(new ShopOffer("ember", "Ember", 300, OfferState.ForSale), Themes[1]);
    }

    [Fact]
    public void Owned_item_not_chosen_is_owned()
    {
        Own("tide");

        Assert.Equal(OfferState.Owned, Themes[2].State);
    }

    [Fact]
    public void Chosen_item_is_in_use()
    {
        Own("tide", choose: true);

        Assert.Equal(OfferState.InUse, Themes[2].State);
    }

    [Fact]
    public void Unknown_chosen_theme_shows_the_default_in_use()
    {
        Own("retired-theme", choose: true);

        Assert.Equal((OfferState.InUse, OfferState.ForSale), (Themes[0].State, Themes[1].State));
    }

    [Fact]
    public void Looks_use_the_chosen_look()
    {
        save.AddOwned("ring");
        save.ChosenLook = "ring";

        Assert.Equal(
            [OfferState.Free, OfferState.InUse],
            ShopOffers.Looks(Catalogue, Shop, save).Select(offer => offer.State));
    }

    [Fact]
    public void ShortBy_is_the_difference_when_short()
    {
        Assert.Equal(60L, ShopOffers.ShortBy(Themes[1], 240));
    }

    [Theory]
    [InlineData(300)]
    [InlineData(1240)]
    public void ShortBy_is_zero_when_affordable(long balance)
    {
        Assert.Equal(0L, ShopOffers.ShortBy(Themes[1], balance));
    }

    [Fact]
    public void ShortBy_is_zero_for_an_item_not_for_sale()
    {
        Assert.Equal(0L, ShopOffers.ShortBy(Themes[0], 0));
    }
}
