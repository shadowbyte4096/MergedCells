namespace MergedCells.Core;

public sealed class Attempt
{
    public Attempt(Map map)
    {
        ArgumentNullException.ThrowIfNull(map);
        Map = map;
        Position = map.Start;
    }

    public Map Map { get; }

    public Cell Position { get; private set; }

    public int Moves { get; private set; }

    public bool IsWon { get; private set; }

    /// <summary>
    /// Slides the player and counts the move. Returns <see cref="MoveOutcome.Ignored"/>, changing nothing,
    /// when the slide wouldn't move the player or the attempt is already won (an input buffered during
    /// the winning slide's animation can still arrive).
    /// </summary>
    public MoveOutcome Move(Direction direction)
    {
        if (IsWon)
        {
            return MoveOutcome.Ignored;
        }

        var slide = Map.Slide(Position, direction);
        if (slide.Stop == Position && !slide.ReachedEnd)
        {
            return MoveOutcome.Ignored;
        }

        Moves++;
        Position = slide.Stop;
        IsWon = slide.ReachedEnd;
        return IsWon ? MoveOutcome.Won : MoveOutcome.Moved;
    }
}
