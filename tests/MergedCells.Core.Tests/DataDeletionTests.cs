using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

public class DataDeletionTests
{
    [Theory]
    [AutoData]
    public void Deleting_drops_the_player_and_the_records(Guid player)
    {
        var save = new SaveGame { Player = player };
        var recordsDeleted = false;

        var result = DataDeletion.Run(save, () => true, () =>
        {
            recordsDeleted = true;
            return null;
        });

        Assert.Equal((DeletionOutcome.Deleted, (Guid?)null, true), (result.Outcome, save.Player, recordsDeleted));
    }

    // A "deleted" ID must never come back next run, so nothing is deleted until the save without it is written
    [Theory]
    [AutoData]
    public void Save_that_cannot_be_written_keeps_the_player_and_the_records(Guid player)
    {
        var save = new SaveGame { Player = player };
        var recordsDeleted = false;

        var result = DataDeletion.Run(save, () => false, () =>
        {
            recordsDeleted = true;
            return null;
        });

        Assert.Equal((DeletionOutcome.SaveNotWritten, (Guid?)player, false), (result.Outcome, save.Player, recordsDeleted));
    }

    [Theory]
    [AutoData]
    public void Records_that_cannot_be_deleted_are_reported_after_the_player_is_dropped(Guid player, string problem)
    {
        var save = new SaveGame { Player = player };

        var result = DataDeletion.Run(save, () => true, () => problem);

        Assert.Equal((DeletionOutcome.RecordsKept, problem, (Guid?)null), (result.Outcome, result.Problem, save.Player));
    }

    // The save is written without the ID before the records go
    [Theory]
    [AutoData]
    public void Save_is_written_before_the_records_are_deleted(Guid player)
    {
        var save = new SaveGame { Player = player };
        var order = new List<string>();

        DataDeletion.Run(save, () =>
        {
            order.Add(save.Player is null ? "save without the player" : "save with the player");
            return true;
        }, () =>
        {
            order.Add("records");
            return null;
        });

        Assert.Equal(["save without the player", "records"], order);
    }

    [Theory]
    [AutoData]
    public void Signed_in_device_queues_the_old_player_for_the_server(Guid player, string token, uint sent)
    {
        var save = new SaveGame { Player = player, SessionToken = token, RecordsSent = sent };

        var result = DataDeletion.Run(save, () => true, () => null);

        Assert.Equal(
            (true, true, (string?)null, 0L),
            (result.ServerPending, save.PendingDeletions.Contains(player), save.SessionToken, save.RecordsSent));
    }

    [Theory]
    [AutoData]
    public void Device_that_never_signed_in_queues_nothing(Guid player)
    {
        var save = new SaveGame { Player = player };

        var result = DataDeletion.Run(save, () => true, () => null);

        Assert.Equal((false, 0), (result.ServerPending, save.PendingDeletions.Count));
    }

    // One write, so a run killed after it can't leave the ID dropped without the server's deletion queued
    [Theory]
    [AutoData]
    public void Pending_deletion_and_dropped_session_are_in_the_same_save_write(Guid player, string token)
    {
        var save = new SaveGame { Player = player, SessionToken = token };
        (bool Pending, string? Session)? written = null;

        DataDeletion.Run(save, () =>
        {
            written = (save.PendingDeletions.Contains(player), save.SessionToken);
            return true;
        }, () => null);

        Assert.Equal((true, (string?)null), written);
    }

    [Theory]
    [AutoData]
    public void Save_that_cannot_be_written_puts_back_the_session_the_offset_and_the_deletions(Guid player, string token, uint sent)
    {
        var save = new SaveGame { Player = player, SessionToken = token, RecordsSent = sent };

        var result = DataDeletion.Run(save, () => false, () => null);

        Assert.Equal(
            (false, token, (long)sent, 0),
            (result.ServerPending, save.SessionToken, save.RecordsSent, save.PendingDeletions.Count));
    }

    // The server deletes the published levels with the player, so the save stops claiming them in the same write
    [Theory]
    [AutoData]
    public void Published_is_cleared_in_the_same_write_as_the_player(Guid player, Guid level)
    {
        var save = new SaveGame { Player = player };
        save.RecordPublished(level, DateTimeOffset.UnixEpoch);
        int? written = null;

        DataDeletion.Run(save, () =>
        {
            written = save.Published.Count;
            return true;
        }, () => null);

        Assert.Equal(0, written);
    }

    [Theory]
    [AutoData]
    public void Published_is_back_when_the_save_cannot_be_written(Guid player, Guid level)
    {
        var save = new SaveGame { Player = player };
        save.RecordPublished(level, DateTimeOffset.UnixEpoch);

        DataDeletion.Run(save, () => false, () => null);

        Assert.Equal([level], save.Published.Keys);
    }

    [Fact]
    public void Answers_stay()
    {
        var save = new SaveGame();
        var answer = new Consent(true, ConsentWordings.PlayData.Version);
        save.SetConsent(ConsentKind.PlayData, answer);

        DataDeletion.Run(save, () => true, () => null);

        Assert.Equal(answer, save.ConsentTo(ConsentKind.PlayData));
    }
}
