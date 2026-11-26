namespace MergedCells.Core;

/// <summary>
/// The config's <c>shop</c> group: each cosmetic's price in coins, by its ID. The free defaults have none;
/// <see cref="Cosmetics.CheckPrices"/> checks the prices against the catalogue at start-up.
/// </summary>
public sealed record ShopConfig
{
    public const int MaxPrice = 100_000;

    public required IReadOnlyDictionary<string, int> Prices { get; init; }

    public int? PriceOf(string id) => Prices.TryGetValue(id, out var price) ? price : null;

    /// <summary>Throws <see cref="ArgumentOutOfRangeException"/> naming the first price outside 1 to 100,000.</summary>
    public void CheckRanges()
    {
        foreach (var (id, price) in Prices)
        {
            if (price is < 1 or > MaxPrice)
            {
                throw new ArgumentOutOfRangeException($"shop.prices.{id}", price, $"Must be from 1 to {MaxPrice:N0}.");
            }
        }
    }
}
