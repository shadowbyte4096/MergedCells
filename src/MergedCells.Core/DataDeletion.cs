namespace MergedCells.Core;

public enum DeletionOutcome
{
    Deleted,

    // Nothing was deleted: the save without the player ID couldn't be written
    SaveNotWritten,

    // The player ID is gone, but the records file couldn't be deleted
    RecordsKept,
}

public sealed record DeletionResult(DeletionOutcome Outcome, string? Problem);

/// <summary>
/// "Delete my data" on the device: the player ID, then the play records. The save without the ID is written first, and the
/// records go only once it is, so a "deleted" ID can never come back on the next run. Every answer stays.
/// </summary>
public static class DataDeletion
{
    // writeSave: whether the save was written. deleteRecords: the problem, or null once the records are gone
    public static DeletionResult Run(SaveGame save, Func<bool> writeSave, Func<string?> deleteRecords)
    {
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(writeSave);
        ArgumentNullException.ThrowIfNull(deleteRecords);
        var player = save.Player;
        save.Player = null;
        if (!writeSave())
        {
            save.Player = player;
            return new DeletionResult(DeletionOutcome.SaveNotWritten, null);
        }
        return deleteRecords() is { } problem
            ? new DeletionResult(DeletionOutcome.RecordsKept, problem)
            : new DeletionResult(DeletionOutcome.Deleted, null);
    }
}
