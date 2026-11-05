namespace MergedCells.Core;

/// <summary>
/// One play of a level as the player sees it: an <see cref="Attempt"/>, the slide being animated, and at most one move
/// remembered from during that slide. It applies the campaign's rules: unlimited undo, but none once the level is won.
/// </summary>
public sealed class PlaySession
{
    private Attempt attempt;
    private Direction? remembered;

    public PlaySession(Map map)
    {
        ArgumentNullException.ThrowIfNull(map);
        Map = map;
        attempt = new Attempt(map);
    }

    public Map Map { get; }

    // Where the player is, or is sliding to: a move counts the moment its slide starts
    public Cell Position => attempt.Position;

    public int Moves => attempt.Moves;

    public bool IsWon => attempt.IsWon;

    public bool IsFinished => IsWon && Current is null;

    public PlayerSlide? Current { get; private set; }

    /// <summary>
    /// With no slide in flight, makes the move and returns its slide, or null for a no-op. During a slide, remembers
    /// the move if none is remembered yet (later ones are dropped) and returns null.
    /// </summary>
    public PlayerSlide? Request(Direction direction)
    {
        if (Current is not null)
        {
            remembered ??= direction;
            return null;
        }
        return Start(direction);
    }

    /// <summary>
    /// Ends the slide in flight, then makes the remembered move, if any. Returns that move's slide, or null when there
    /// is none or it's a no-op. With no slide in flight it does nothing and returns null.
    /// </summary>
    public PlayerSlide? FinishSlide()
    {
        if (Current is null)
        {
            return null;
        }

        Current = null;
        var next = remembered;
        remembered = null;
        return next is { } direction ? Start(direction) : null;
    }

    /// <summary>
    /// Takes back the last move, including the one in flight, stops the slide and drops any remembered move. Returns
    /// the undone direction, or null when there's nothing to undo or the level is won (a win is final in the campaign).
    /// </summary>
    public Direction? Undo()
    {
        if (IsWon)
        {
            return null;
        }

        var undone = attempt.Undo();
        Current = null;
        remembered = null;
        return undone;
    }

    // A new Attempt: back to the start with no moves, no history, no slide and no remembered move
    public void Restart()
    {
        attempt = new Attempt(Map);
        Current = null;
        remembered = null;
    }

    private PlayerSlide? Start(Direction direction)
    {
        var from = attempt.Position;
        if (attempt.Move(direction) == MoveOutcome.Ignored)
        {
            return null;
        }

        Current = new PlayerSlide(from, attempt.Position, attempt.IsWon);
        return Current;
    }
}
