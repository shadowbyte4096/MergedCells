namespace MergedCells.Core;

public enum OfferState
{
    // The free default, while something else is worn
    Free,
    ForSale,
    // Bought and not worn
    Owned,
    InUse,
}

/// <summary>One row of the shop: an item, its price (null for a free default) and what the player can do with it.</summary>
public sealed record ShopOffer(string Id, string Name, int? Price, OfferState State);

/// <summary>The shop's rows, worked out from the catalogue, the prices and the save, so the screen holds no rules.</summary>
public static class ShopOffers
{
    public static IReadOnlyList<ShopOffer> Themes(Cosmetics cosmetics, ShopConfig shop, SaveGame save)
    {
        ArgumentNullException.ThrowIfNull(cosmetics);
        ArgumentNullException.ThrowIfNull(save);
        var worn = cosmetics.ThemeFor(save.ChosenTheme).Id;
        return cosmetics.Themes.Select(theme => Offer(theme.Id, theme.Name, worn, cosmetics, shop, save)).ToArray();
    }

    public static IReadOnlyList<ShopOffer> Looks(Cosmetics cosmetics, ShopConfig shop, SaveGame save)
    {
        ArgumentNullException.ThrowIfNull(cosmetics);
        ArgumentNullException.ThrowIfNull(save);
        var worn = cosmetics.LookFor(save.ChosenLook).Id;
        return cosmetics.Looks.Select(look => Offer(look.Id, look.Name, worn, cosmetics, shop, save)).ToArray();
    }

    /// <summary>How many more coins an item for sale needs, or 0 when it's affordable or not for sale.</summary>
    public static long ShortBy(ShopOffer offer, long balance)
    {
        ArgumentNullException.ThrowIfNull(offer);
        return offer is { State: OfferState.ForSale, Price: { } price } ? Math.Max(0, price - balance) : 0;
    }

    private static ShopOffer Offer(string id, string name, string worn, Cosmetics cosmetics, ShopConfig shop, SaveGame save)
    {
        ArgumentNullException.ThrowIfNull(shop);
        var free = cosmetics.IsFree(id);
        var state = id == worn ? OfferState.InUse
            : free ? OfferState.Free
            : save.Owned.Contains(id) ? OfferState.Owned
            : OfferState.ForSale;
        return new ShopOffer(id, name, free ? null : shop.PriceOf(id), state);
    }
}
