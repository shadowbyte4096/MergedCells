namespace MergedCells.Core;

/// <summary>
/// The config's <c>infinite.scoring</c> group and infinite mode's scoring formulas: a map's efficiency in hundredths of a
/// percent, the streak bonus, and its points (par × <see cref="PointsPerPar"/> × efficiency × (1 + bonus)). Whole numbers
/// only, each rounded down once.
/// </summary>
public sealed record InfiniteScoring
{
    public required int PointsPerPar { get; init; }

    // Percent added per map already in the streak, up to the cap
    public required int StreakBonusPercent { get; init; }

    public required int StreakBonusCapPercent { get; init; }

    // How many maps in a row the best average efficiency is taken over
    public required int EfficiencyWindow { get; init; }

    /// <summary>Par ÷ moves in hundredths of a percent, rounded down, so only a finish at par is 10000.</summary>
    public int Efficiency(int par, int moves)
    {
        Check(par, moves);
        return (int)(par * 10000L / moves);
    }

    /// <summary>The bonus in percent for a map played with <paramref name="streakBefore"/> maps already in the streak.</summary>
    public int BonusPercent(int streakBefore)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(streakBefore);
        return (int)Math.Min((long)streakBefore * StreakBonusPercent, StreakBonusCapPercent);
    }

    /// <summary>A finished map's points, rounded down.</summary>
    public long Points(int par, int moves, int streakBefore)
    {
        Check(par, moves);
        // par² × 1000 × 1100 overflows a long at large pars, though the answer never does
        Int128 product = (Int128)par * PointsPerPar * par * (100 + BonusPercent(streakBefore));
        return (long)(product / ((Int128)moves * 100));
    }

    /// <summary>
    /// Throws <see cref="ArgumentOutOfRangeException"/> naming the first value out of range. <see cref="InfiniteConfig.CheckRanges"/>
    /// calls it.
    /// </summary>
    public void CheckRanges()
    {
        Check(PointsPerPar, 1, "infinite.scoring.pointsPerPar");
        Check(StreakBonusPercent, 0, "infinite.scoring.streakBonusPercent");
        Check(StreakBonusCapPercent, 0, "infinite.scoring.streakBonusCapPercent");
        Check(EfficiencyWindow, 1, "infinite.scoring.efficiencyWindow");
    }

    private static void Check(int value, int min, string name)
    {
        if (value < min || value > 1000)
        {
            throw new ArgumentOutOfRangeException(name, value, $"Must be from {min} to 1000.");
        }
    }

    private static void Check(int par, int moves)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(par, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(moves, par);
    }
}
