using System.Globalization;

namespace MergedCells.Core;

/// <summary>One of the three numbers on an infinite map's card or the setup screen: a small key, a big value and a note under it.</summary>
public sealed record StatTile(string Key, string Value, string Note, bool Strong);

/// <summary>
/// The words on an infinite map's results card: the par line, the map's best line (null on its first finish), and the
/// streak, run and window tiles, each with a note giving the best for the choice or saying a best has just been set.
/// </summary>
public sealed record InfiniteCard(string ParLine, string? BestLine, StatTile Streak, StatTile Run, StatTile Efficiency)
{
    /// <param name="before">The choice's bests before this finish, null when none were saved.</param>
    /// <param name="after">The choice's bests after this finish was recorded.</param>
    public static InfiniteCard Of(MapScore score, InfiniteRun run, InfiniteBest? before, InfiniteBest after, InfiniteScoring scoring)
    {
        ArgumentNullException.ThrowIfNull(score);
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(scoring);
        var was = before ?? InfiniteBest.None;
        var parLine = $"Par {Count(score.Par)} · {Percent(score.Efficiency)} efficient";
        if (score.OverLimit)
        {
            parLine += $" · streak at {Count(score.StreakLimit)}";
        }
        string? bestLine = score.PreviousMoves switch
        {
            null => null,
            { } previous when score.Counted => $"Improved · was {Moves(previous)}",
            _ => $"Your best · {Moves(score.KeptMoves)}",
        };
        var streak = run.Streak == 0 && score.StreakBefore > 0
            ? new StatTile("Streak", "0", $"broken · was {Number(score.StreakBefore)}", true)
            : after.Streak > was.Streak
                ? new StatTile("Streak", Number(run.Streak), "New best", true)
                : new StatTile("Streak", Number(run.Streak), $"best {Number(after.Streak)}", false);
        var points = $"+{Number(score.KeptPoints)}";
        var runTile = after.Run > was.Run
            ? new StatTile("Run", Number(run.Score), $"{points} · new best", true)
            : new StatTile("Run", Number(run.Score), $"{points} this map", false);
        return new InfiniteCard(parLine, bestLine, streak, runTile, Window(run, was, after, scoring.EfficiencyWindow));
    }

    /// <summary>The HUD's line during a map: the run's score and streak as they stand.</summary>
    public static string RunLine(InfiniteRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        return $"Run {Number(run.Score)} · streak {Number(run.Streak)}";
    }

    /// <summary>The setup screen's three bests for a choice, or null when it has none, for "No runs at these settings yet".</summary>
    public static IReadOnlyList<StatTile>? SetupTiles(InfiniteBest? best, InfiniteScoring scoring)
    {
        ArgumentNullException.ThrowIfNull(scoring);
        if (best is null)
        {
            return null;
        }
        return
        [
            new StatTile("Streak", Number(best.Streak), "", false),
            new StatTile("Run", Number(best.Run), "", false),
            new StatTile($"Best {Number(scoring.EfficiencyWindow)}", best.Efficiency is { } efficiency ? Percent(efficiency) : "–", "", false),
        ];
    }

    private static StatTile Window(InfiniteRun run, InfiniteBest was, InfiniteBest after, int window)
    {
        var key = $"Last {Number(window)}";
        if (run.WindowAverage is not { } average)
        {
            return new StatTile(key, $"{Number(run.Maps)} of {Number(window)}", $"so far {Percent(run.RunningAverage)}", false);
        }
        return after.Efficiency > (was.Efficiency ?? -1)
            ? new StatTile(key, Percent(average), "New best", true)
            : new StatTile(key, Percent(average), $"best {Percent(after.Efficiency ?? average)}", false);
    }

    // Hundredths of a percent, shown whole and rounded down
    private static string Percent(int hundredths) => Number(hundredths / 100) + "%";

    private static string Number(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

    private static string Moves(int moves) => moves == 1 ? "1 move" : $"{Count(moves)} moves";

    // Moves and par as the headline and the HUD write them, with no separator
    private static string Count(int value) => value.ToString(CultureInfo.InvariantCulture);
}
