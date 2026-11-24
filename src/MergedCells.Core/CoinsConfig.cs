namespace MergedCells.Core;

/// <summary>
/// The config's <c>coins</c> group and the earning formulas: coins per newly earned campaign star, the daily's flat
/// payout, and a map's payout (par × <see cref="PerPar"/> × efficiency × <see cref="PercentPerScore"/>% of its difficulty
/// score), rounded down once.
/// </summary>
public sealed record CoinsConfig
{
    public required int PerStar { get; init; }

    public required int Daily { get; init; }

    public required int PerPar { get; init; }

    // Percent of the map's difficulty score that multiplies its payout: 5 makes score 100 worth 5x
    public required int PercentPerScore { get; init; }

    public long ForStars(int newStars)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(newStars);
        return (long)newStars * PerStar;
    }

    /// <summary>A finished map's payout: par × perPar × (par ÷ moves) × score × percentPerScore ÷ 100, rounded down.</summary>
    public long ForMap(int par, int moves, int score)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(par, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(moves, par);
        ArgumentOutOfRangeException.ThrowIfNegative(score);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(score, 100);
        // par² × 1000 × 100 × 1000 overflows a long at large pars, though the answer never does
        Int128 product = (Int128)PerPar * par * par * score * PercentPerScore;
        return (long)(product / ((Int128)moves * 100));
    }

    /// <summary>
    /// Throws <see cref="ArgumentOutOfRangeException"/> naming the first value out of range. <see cref="GameConfig.CheckRanges"/>
    /// calls it at start-up.
    /// </summary>
    public void CheckRanges()
    {
        Check(PerStar, "coins.perStar");
        Check(Daily, "coins.daily");
        Check(PerPar, "coins.perPar");
        Check(PercentPerScore, "coins.percentPerScore");
    }

    private static void Check(int value, string name)
    {
        if (value is < 0 or > 1000)
        {
            throw new ArgumentOutOfRangeException(name, value, "Must be from 0 to 1000.");
        }
    }
}
