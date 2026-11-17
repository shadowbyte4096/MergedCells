namespace MergedCells.Core;

/// <summary>The level editor's undo and redo: the current map and the maps before and after it.</summary>
public sealed class EditHistory(Map map)
{
    // A memory bound, not game feel: maps are small, and nobody undoes 200 strokes
    public const int Depth = 200;

    private readonly LinkedList<Map> undo = new();
    private readonly Stack<Map> redo = new();

    public Map Current { get; private set; } = map ?? throw new ArgumentNullException(nameof(map));

    public bool CanUndo => undo.Count > 0;

    public bool CanRedo => redo.Count > 0;

    public void Apply(Map map)
    {
        ArgumentNullException.ThrowIfNull(map);
        undo.AddLast(Current);
        if (undo.Count > Depth)
        {
            undo.RemoveFirst();
        }
        redo.Clear();
        Current = map;
    }

    public bool Undo()
    {
        if (undo.Last is not { } previous)
        {
            return false;
        }
        undo.RemoveLast();
        redo.Push(Current);
        Current = previous.Value;
        return true;
    }

    public bool Redo()
    {
        if (!redo.TryPop(out var next))
        {
            return false;
        }
        undo.AddLast(Current);
        Current = next;
        return true;
    }
}
