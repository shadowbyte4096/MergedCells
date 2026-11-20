namespace MergedCells.Core;

public enum DeletionOutcome
{
    Deleted,

    // Nothing was deleted: the save without the player ID couldn't be written
    SaveNotWritten,

    // The player ID is gone, but the records file couldn't be deleted
    RecordsKept,
}

// ServerPending: the old player ID is in the save's pending deletions, for the server to delete everything under it
public sealed record DeletionResult(DeletionOutcome Outcome, string? Problem, bool ServerPending);

/// <summary>
/// "Delete my data" on the device: the player ID, then the play records. The save without the ID is written first, and the
/// records go only once it is, so a "deleted" ID can never come back on the next run. In that same write a device that
/// signed in to the server drops its session and queues the old ID for the server to delete. Every answer stays.
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
        var session = save.SessionToken;
        var sent = save.RecordsSent;
        // Only a device that signed in since the last deletion can have sent anything under this ID
        var serverPending = player is { } id && session is not null && save.AddPendingDeletion(id);
        save.Player = null;
        save.SessionToken = null;
        save.RecordsSent = 0;
        if (!writeSave())
        {
            save.Player = player;
            save.SessionToken = session;
            save.RecordsSent = sent;
            if (serverPending)
            {
                save.RemovePendingDeletion(player!.Value);
            }
            return new DeletionResult(DeletionOutcome.SaveNotWritten, null, false);
        }
        return deleteRecords() is { } problem
            ? new DeletionResult(DeletionOutcome.RecordsKept, problem, serverPending)
            : new DeletionResult(DeletionOutcome.Deleted, null, serverPending);
    }
}
