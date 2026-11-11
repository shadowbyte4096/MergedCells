namespace MergedCells.Core;

public sealed class Attempt
{
    // One entry per counted move: where it started, and its direction for later move lists
    private readonly Stack<(Cell From, Direction Direction)> history = new();

    public Attempt(Map map)
    {
        ArgumentNullException.ThrowIfNull(map);
        Map = map;
        Position = map.Start;
    }

    public Map Map { get; }

    public Cell Position { get; private set; }

    public int Moves => history.Count;

    public bool IsWon { get; private set; }

    /// <summary>
    /// Slides the player and counts the move. Returns <see cref="MoveOutcome.Ignored"/>, changing nothing,
    /// when the slide wouldn't move the player or the attempt is already won (an input buffered during
    /// the winning slide's animation can still arrive). A trap or a teleporter that brings the player back to where they
    /// began still counts. A slide that would never stop is ignored.
    /// </summary>
    public MoveOutcome Move(Direction direction)
    {
        if (IsWon)
        {
            return MoveOutcome.Ignored;
        }

        var slide = Map.Slide(Position, direction);
        // A looping slide has every flag false, so it's ignored here too
        if (slide.Stop == Position && !slide.ReachedEnd && !slide.Trapped && !slide.Teleported)
        {
            return MoveOutcome.Ignored;
        }

        history.Push((Position, direction));
        Position = slide.Stop;
        IsWon = slide.ReachedEnd;
        return IsWon ? MoveOutcome.Won : MoveOutcome.Moved;
    }

    /// <summary>
    /// Takes back the last counted move and returns its direction, or null when there is nothing to undo.
    /// A winning move can be undone too, which clears <see cref="IsWon"/>; whether a mode allows that is up to its caller.
    /// </summary>
    public Direction? Undo()
    {
        if (!history.TryPop(out var last))
        {
            return null;
        }

        Position = last.From;
        IsWon = false;
        return last.Direction;
    }
}
