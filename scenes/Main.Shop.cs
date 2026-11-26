using System.Linq;
using Godot;
using MergedCells.Core;

namespace MergedCells;

// The theme and look the player wears, handed to every screen, and the shop that changes them
public partial class Main
{
    private AppLooks wornLooks;

    [Export] public PackedScene ShopScene { get; set; }

    public void ShowShop() => Swap(() =>
    {
        var shop = Open<ShopScreen>(ShopScene);
        shop.BackPressed += ShowModeSelect;
        shop.BuyPressed += id => Buy(shop, id);
        shop.UsePressed += id => Use(shop, id);
        var save = SaveGameFile.Current;
        shop.Open(CosmeticsFile.Current, ShopOffers.Themes(CosmeticsFile.Current, GameConfigFile.Current.Shop, save),
            ShopOffers.Looks(CosmeticsFile.Current, GameConfigFile.Current.Shop, save), save.Coins);
        back = ShowModeSelect;
    });

    // From the save; an ID this build doesn't have (a retired item) wears the default
    private void WearCosmetics()
    {
        var catalogue = CosmeticsFile.Current;
        var theme = catalogue.ThemeFor(SaveGameFile.Current.ChosenTheme);
        var look = catalogue.LookFor(SaveGameFile.Current.ChosenLook);
        wornLooks = CosmeticLooks.For(theme, look);
        GD.Print($"Wearing: {theme.Id}, {look.Id}");
    }

    // A second press of Buy in the same frame finds the item owned, so the wallet refuses it and nothing is spent twice
    private void Buy(ShopScreen shop, string id)
    {
        if (GameConfigFile.Current.Shop.PriceOf(id) is not { } price || !Wallet.Buy(id, price))
        {
            return;
        }
        Choose(id);
        SaveGameFile.Save();
        GD.Print($"Shop: bought {id} for {CoinWords.Balance(price)} ({CoinWords.Balance(Wallet.Balance)} left)");
        Rewear(shop);
    }

    private void Use(ShopScreen shop, string id)
    {
        var catalogue = CosmeticsFile.Current;
        if (!catalogue.IsFree(id) && !SaveGameFile.Current.Owned.Contains(id))
        {
            return;
        }
        Choose(id);
        SaveGameFile.Save();
        GD.Print($"Shop: using {id}");
        Rewear(shop);
    }

    // The save names a free default as null
    private static void Choose(string id)
    {
        var catalogue = CosmeticsFile.Current;
        var chosen = catalogue.IsFree(id) ? null : id;
        if (catalogue.Themes.Any(theme => theme.Id == id))
        {
            SaveGameFile.Current.ChosenTheme = chosen;
        }
        else
        {
            SaveGameFile.Current.ChosenLook = chosen;
        }
    }

    private void Rewear(ShopScreen shop)
    {
        WearCosmetics();
        shop.Looks = wornLooks;
        shop.Dark = dark;
        var save = SaveGameFile.Current;
        shop.Refresh(ShopOffers.Themes(CosmeticsFile.Current, GameConfigFile.Current.Shop, save),
            ShopOffers.Looks(CosmeticsFile.Current, GameConfigFile.Current.Shop, save), save.Coins);
    }
}
