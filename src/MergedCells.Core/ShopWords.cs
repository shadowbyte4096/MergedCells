namespace MergedCells.Core;

/// <summary>The shop's words, in one place so they can be translated later. Numbers come from <see cref="CoinWords"/>.</summary>
public static class ShopWords
{
    public const string Free = "Free";

    public const string Owned = "Owned";

    public const string InUse = "In use";

    public const string Use = "Use";

    public static string Buy(int price) => $"Buy · {CoinWords.Balance(price)}";

    public static string Balance(long coins) => coins == 1 ? "You have 1 coin" : $"You have {CoinWords.Balance(coins)} coins";

    public static string Short(long coins) => coins == 1 ? "You need 1 more coin" : $"You need {CoinWords.Balance(coins)} more coins";
}
