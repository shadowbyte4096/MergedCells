namespace MergedCells.Core;

/// <summary>
/// Builds one visit's record as it's played, from the moves, undos and restarts the play screen reports with their times,
/// and hands it to <c>write</c> once, when the visit ends. Anything reported after that is ignored. Main thread only.
/// </summary>
public sealed class PlayRecorder
{
    private readonly PlayRecordStart start;
    private readonly Action<PlayRecord> write;
    private readonly List<PlayAction> actions = [];
    private int undos;
    private int restarts;

    public PlayRecorder(PlayRecordStart start, Action<PlayRecord> write)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(write);
        this.start = start;
        this.write = write;
    }

    public bool Ended { get; private set; }

    public void Move(Direction direction, long ms) => Add(direction switch
    {
        Direction.Up => PlayActionKind.Up,
        Direction.Down => PlayActionKind.Down,
        Direction.Left => PlayActionKind.Left,
        Direction.Right => PlayActionKind.Right,
        _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Not a direction."),
    }, ms);

    public void Undo(long ms)
    {
        if (Add(PlayActionKind.Undo, ms))
        {
            undos++;
        }
    }

    public void Restart(long ms)
    {
        if (Add(PlayActionKind.Restart, ms))
        {
            restarts++;
        }
    }

    public void End(PlayOutcome outcome, int moves, long ms)
    {
        if (Ended)
        {
            return;
        }
        Ended = true;
        write(new PlayRecord(start, outcome, moves, undos, restarts, ms, actions.ToArray()));
    }

    private bool Add(PlayActionKind kind, long ms)
    {
        if (Ended)
        {
            return false;
        }
        actions.Add(new PlayAction(ms, kind));
        return true;
    }
}
