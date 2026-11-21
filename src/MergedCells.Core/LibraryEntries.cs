namespace MergedCells.Core;

/// <summary>
/// One library row the phone has checked: its level as the code holds it with the phone's own status, or no level for a
/// tombstone in the player's own list.
/// </summary>
public sealed record LibraryEntry(Guid Id, CustomLevel? Level, LevelStatus? Status, int Finished, int Likes, bool Mine, bool Hidden, bool Removed);

public static class LibraryEntries
{
    /// <summary>
    /// The row as the phone can trust it, or null when it must be dropped. The server never reads inside a code, so the
    /// code must read, and its ID, name, size, par and layout must be what the row says; the level must be one Import would
    /// take (no side over the maximum, no looping slide, a way to the end). The publisher's score isn't checked: it only
    /// filters, and the entry's band is the phone's own.
    /// </summary>
    public static LibraryEntry? Check(LibraryRow row, DifficultyRater rater, CustomLevelsConfig config)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(rater);
        ArgumentNullException.ThrowIfNull(config);
        if (row.Removed)
        {
            return new LibraryEntry(row.Id, null, null, row.Finished, row.Likes, row.Mine, row.Hidden, true);
        }
        if (row.Code is null || ShareCode.Read(row.Code).Level is not { } level)
        {
            return null;
        }
        var map = level.Map;
        if (level.Id != row.Id || level.Name != row.Name || map.Rows != row.Rows || map.Columns != row.Columns
            || map.Rows > config.MaxSide || map.Columns > config.MaxSide)
        {
            return null;
        }
        var status = LevelCheck.Of(map, rater, config);
        if (!status.IsPlayable || status.Par != row.Par || LevelFingerprint.Of(map, row.Par) != row.Layout)
        {
            return null;
        }
        return new LibraryEntry(row.Id, level, status, row.Finished, row.Likes, row.Mine, row.Hidden, false);
    }

    /// <summary>The rows not already listed, each once: a level whose count changed between pages can come back.</summary>
    public static IReadOnlyList<LibraryRow> Unseen(IReadOnlySet<Guid> shown, IReadOnlyList<LibraryRow> rows)
    {
        ArgumentNullException.ThrowIfNull(shown);
        ArgumentNullException.ThrowIfNull(rows);
        var seen = new HashSet<Guid>(shown);
        return rows.Where(row => seen.Add(row.Id)).ToArray();
    }
}
