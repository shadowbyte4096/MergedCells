namespace MergedCells.Core;

// A mode's rules for one play of a level. Null UndoLimit means unlimited undo.
public sealed record PlayRules
{
    public PlayRules(int? undoLimit)
    {
        if (undoLimit is { } limit)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(limit, nameof(undoLimit));
        }
        UndoLimit = undoLimit;
    }

    public static PlayRules Unlimited { get; } = new((int?)null);

    public int? UndoLimit { get; }
}
