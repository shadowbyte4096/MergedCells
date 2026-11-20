using System.Globalization;

namespace MergedCells.Core;

/// <summary>How many players' bests on a level took <see cref="Moves"/> moves.</summary>
public readonly record struct MoveCount(int Moves, int Players);

/// <summary>
/// The server's spread for one level and layout: the lowest move counts in ascending order, and <see cref="More"/>, the
/// players whose moves are above every listed count.
/// </summary>
public sealed record SpreadCounts(IReadOnlyList<MoveCount> Counts, int More);

/// <summary>
/// The chart's columns, known before the server answers: one per move count from <see cref="Par"/>, the last holding that
/// many moves or more, and the column the player's <see cref="Best"/> falls in. Made only through <see cref="Of"/>, so every
/// axis has passed its checks.
/// </summary>
public sealed record SpreadAxis
{
    private SpreadAxis(int par, int best, int columns)
    {
        Par = par;
        Best = best;
        Columns = columns;
    }

    public int Par { get; }

    public int Best { get; }

    public int Columns { get; }

    public int BestColumn => Math.Min(Best - Par, Columns - 1);

    public static SpreadAxis Of(int par, int best, int columns)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(par, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(best, par);
        ArgumentOutOfRangeException.ThrowIfLessThan(columns, 2);
        return new SpreadAxis(par, best, columns);
    }

    public string Label(int column)
    {
        var moves = (Par + column).ToString(CultureInfo.InvariantCulture);
        return column == Columns - 1 ? moves + "+" : moves;
    }
}

/// <summary>One column: its players, its share as text ("41%", "&lt;1%") and its bar's height against the largest column's.</summary>
public sealed record SpreadColumn(int Players, string Share, float Height);

/// <summary>
/// The results card's chart of everyone's best moves, worked out from the server's counts: each column's share, the player
/// count, and the line saying where the player's best sits. <see cref="Line"/> is null while the player is the only one.
/// </summary>
public sealed record BestMovesSpread(SpreadAxis Axis, IReadOnlyList<SpreadColumn> Columns, int Players, string PlayersText, string? Line)
{
    public bool IsFirst => Players <= 1;

    public static BestMovesSpread Of(SpreadCounts counts, SpreadAxis axis)
    {
        ArgumentNullException.ThrowIfNull(counts);
        ArgumentNullException.ThrowIfNull(axis);
        var players = new int[axis.Columns];
        var behind = counts.More;
        // Scores below par are refused by the server, so only a damaged reply could hold one
        foreach (var count in counts.Counts.Where(count => count.Moves >= axis.Par))
        {
            players[Math.Min(count.Moves - axis.Par, axis.Columns - 1)] += count.Players;
            if (count.Moves > axis.Best)
            {
                behind += count.Players;
            }
        }
        players[^1] += counts.More;
        var total = players.Sum();
        var largest = players.Max();
        var columns = players
            .Select(column => new SpreadColumn(column, Share(column, total), largest == 0 ? 0 : (float)column / largest))
            .ToArray();
        var spread = new BestMovesSpread(axis, columns, total, CountText(total), null);
        return spread.IsFirst ? spread : spread with { Line = LineFor(axis, columns, behind, total) };
    }

    private static string LineFor(SpreadAxis axis, SpreadColumn[] columns, int behind, int total)
    {
        if (axis.Best == axis.Par)
        {
            return $"Matched par, like {columns[0].Share} of players";
        }
        if (behind > 0)
        {
            // Rounded down, so it never flatters
            var percent = (int)((long)behind * 100 / total);
            return $"Better than {(percent == 0 ? "<1" : percent.ToString(CultureInfo.InvariantCulture))}% of players";
        }
        return columns[0].Players > 0 ? $"{columns[0].Share} of players found par" : "Nobody has found par yet";
    }

    // Whole percentages rounded half up, and "<1%" for a column with anyone in it
    private static string Share(int players, int total)
    {
        if (players == 0)
        {
            return "0%";
        }
        var percent = (int)((200L * players + total) / (2L * total));
        return percent == 0 ? "<1%" : percent.ToString(CultureInfo.InvariantCulture) + "%";
    }

    private static string CountText(int total) => total switch
    {
        0 => "",
        1 => "1 player",
        _ => total.ToString("N0", CultureInfo.InvariantCulture) + " players",
    };
}
