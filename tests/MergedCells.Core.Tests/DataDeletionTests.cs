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
