using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

public class PlayRecorderTests
{
    private readonly List<PlayRecord> written = [];

    private PlayRecorder Recorder() => new(PlayRecords.Infinite(TestMaps.Example()), written.Add);

    [Theory]
    [AutoData]
    public void Actions_are_kept_in_order_with_their_times(long up, long down, long left, long right, long undo, long restart)
    {
        var recorder = Recorder();
        recorder.Move(Direction.Up, up);
        recorder.Move(Direction.Down, down);
        recorder.Move(Direction.Left, left);
        recorder.Move(Direction.Right, right);
        recorder.Undo(undo);
        recorder.Restart(restart);

        recorder.End(PlayOutcome.Won, 3, restart);

        Assert.Equal(
            [
                new PlayAction(up, PlayActionKind.Up),
                new PlayAction(down, PlayActionKind.Down),
                new PlayAction(left, PlayActionKind.Left),
                new PlayAction(right, PlayActionKind.Right),
                new PlayAction(undo, PlayActionKind.Undo),
                new PlayAction(restart, PlayActionKind.Restart),
            ],
            written.Single().Actions);
    }

    [Fact]
    public void Undos_and_restarts_are_counted()
    {
        var recorder = Recorder();
        recorder.Undo(1);
        recorder.Undo(2);
        recorder.Restart(3);

        recorder.End(PlayOutcome.Left, 0, 4);

        Assert.Equal((2, 1), (written.Single().Undos, written.Single().Restarts));
    }

    [Theory]
    [InlineData(PlayOutcome.Won)]
    [InlineData(PlayOutcome.GaveUp)]
    [InlineData(PlayOutcome.Left)]
    public void Outcome_moves_and_time_are_recorded(PlayOutcome outcome)
    {
        var recorder = Recorder();
        recorder.Move(Direction.Up, 1);

        recorder.End(outcome, 7, 4321);

        Assert.Equal((outcome, 7, 4321L), (written.Single().Outcome, written.Single().Moves, written.Single().ActiveMs));
    }

    [Theory]
    [InlineData(PlayOutcome.Won)]
    [InlineData(PlayOutcome.GaveUp)]
    [InlineData(PlayOutcome.Left)]
    public void Visit_with_nothing_done_writes_nothing(PlayOutcome outcome)
    {
        var recorder = Recorder();

        recorder.End(outcome, 0, 4321);

        Assert.Empty(written);
    }

    [Fact]
    public void End_writes_the_record_once()
    {
        var recorder = Recorder();
        recorder.Move(Direction.Up, 1);

        recorder.End(PlayOutcome.Won, 3, 10);
        recorder.End(PlayOutcome.Left, 3, 20);

        Assert.Single(written);
    }

    [Fact]
    public void Calls_after_end_are_ignored()
    {
        var recorder = Recorder();
        recorder.Move(Direction.Up, 1);
        recorder.End(PlayOutcome.Won, 3, 10);

        recorder.Move(Direction.Up, 11);
        recorder.Undo(12);
        recorder.Restart(13);

        Assert.Equal((1, 0, 0), (written.Single().Actions.Count, written.Single().Undos, written.Single().Restarts));
    }

    [Fact]
    public void Ended_is_set_by_end()
    {
        var recorder = Recorder();

        recorder.End(PlayOutcome.GaveUp, 0, 1);

        Assert.True(recorder.Ended);
    }
}
