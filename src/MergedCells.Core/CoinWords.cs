using System.Globalization;

namespace MergedCells.Core;

/// <summary>The words for coins on screen, kept in one place so they can be translated later.</summary>
public static class CoinWords
{
    public static string Balance(long coins) => coins.ToString("N0", CultureInfo.InvariantCulture);

    public static string Earned(long coins) => coins == 1 ? "+1 coin" : $"+{Balance(coins)} coins";
}
