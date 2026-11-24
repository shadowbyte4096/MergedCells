namespace MergedCells.Core;

/// <summary>
/// The one place coins are earned: each method works out what a finish pays, adds it to the save's balance and returns
/// it, so the caller shows it and writes the save once. Nothing here writes the file. Main thread only.
/// </summary>
public sealed class Wallet(SaveGame save, CoinsConfig config)
{
    public long Balance => save.Coins;

    /// <summary>A campaign finish pays only for stars above the level's previous best, so a replay never pays twice.</summary>
    public long PayStars(LevelFinish finish)
    {
        ArgumentNullException.ThrowIfNull(finish);
        return Pay(config.ForStars(finish.NewStars));
    }

    /// <summary>
    /// An infinite map pays when its finish counts, as a top-up: the payout at the kept moves less the payout at the moves it
    /// kept before, so a Retry pays only the rise.
    /// </summary>
    public long PayMap(MapScore score, int difficultyScore)
    {
        ArgumentNullException.ThrowIfNull(score);
        if (!score.Counted)
        {
            return 0;
        }
        var earlier = score.PreviousMoves is { } previous ? config.ForMap(score.Par, previous, difficultyScore) : 0;
        return Pay(Math.Max(0, config.ForMap(score.Par, score.KeptMoves, difficultyScore) - earlier));
    }

    /// <summary>
    /// An imported custom level pays as a top-up over the most it has ever paid under its ID, across versions and
    /// re-imports. A level made on this device never pays.
    /// </summary>
    public long PayCustom(CustomLevel level, int par, int moves, int difficultyScore)
    {
        ArgumentNullException.ThrowIfNull(level);
        if (!level.IsImported)
        {
            return 0;
        }
        var payout = config.ForMap(par, moves, difficultyScore);
        var best = save.CustomPayout(level.Id) ?? 0;
        if (payout <= best)
        {
            return 0;
        }
        save.SetCustomPayout(level.Id, payout);
        return Pay(payout - best);
    }

    // For chunk 25's daily puzzle: its once-a-day rule is that chunk's
    public long PayDaily() => Pay(config.Daily);

    private long Pay(long amount)
    {
        if (amount > 0)
        {
            save.AddCoins(amount);
        }
        return amount;
    }
}
