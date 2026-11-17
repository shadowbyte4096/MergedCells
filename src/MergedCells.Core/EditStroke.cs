using System.Numerics;

namespace MergedCells.Core;

/// <summary>
/// A finished stroke: every square it crossed in order, repeats included, so each square shares an edge with the one
/// before; or just the touch-down square for a tap.
/// </summary>
public sealed record StrokeEnd(IReadOnlyList<Cell> Cells, bool IsTap);

/// <summary>
/// Turns one finger's touch into a level editor stroke, as <see cref="SwipeDetector"/> turns one into a move. A stroke
/// starts when the first finger goes down on the board and ends when it lifts: a tap if it never moved the slop distance
/// from its touch-down, otherwise a drag with every square it crossed, gaps filled by <see cref="CellPath.Line"/>. A second
/// finger cancels it, because two fingers belong to the camera, and nothing starts again until every finger has lifted.
/// Positions are in viewport units; a null square is off the board.
/// </summary>
public sealed class EditStroke
{
    private readonly float slop;
    private readonly HashSet<int> down = [];
    private readonly List<Cell> cells = [];
    private readonly HashSet<Cell> crossed = [];
    // The whole walk, repeats included: without them a finger that doubles back and turns leaves a gap between squares
    private readonly List<Cell> walk = [];
    private int? finger;
    private Vector2 touchDown;
    private bool moved;
    private Cell last;

    public EditStroke(float slop)
    {
        if (!float.IsFinite(slop) || slop < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(slop), slop, "Must be zero or more, and finite.");
        }
        this.slop = slop;
    }

    public bool IsActive => finger is not null;

    // The stroke's squares so far, each once, for drawing it
    public IReadOnlyList<Cell> Cells => cells;

    public void Press(int finger, float x, float y, Cell? cell)
    {
        // A touch-down for a finger already down means its release was lost: the stroke it was making is dropped
        if (!down.Add(finger))
        {
            Clear();
        }
        if (down.Count > 1)
        {
            Clear();
            return;
        }
        if (cell is not { } square)
        {
            return;
        }
        this.finger = finger;
        touchDown = new Vector2(x, y);
        moved = false;
        last = square;
        cells.Add(square);
        crossed.Add(square);
        walk.Add(square);
    }

    /// <summary>The stroke's finger moved. Returns the squares this drag added, empty until it's past the slop.</summary>
    public IReadOnlyList<Cell> Drag(int finger, float x, float y, Cell? cell)
    {
        if (this.finger != finger)
        {
            return [];
        }
        moved = moved || Vector2.Distance(touchDown, new Vector2(x, y)) > slop;
        if (!moved || cell is not { } square)
        {
            return [];
        }
        var added = new List<Cell>();
        foreach (var step in CellPath.Line(last, square).Skip(1))
        {
            walk.Add(step);
            if (crossed.Add(step))
            {
                cells.Add(step);
                added.Add(step);
            }
        }
        last = square;
        return added;
    }

    /// <summary>A finger lifted. Returns the finished stroke when it was the stroke's finger, or null.</summary>
    public StrokeEnd? Release(int finger)
    {
        down.Remove(finger);
        if (this.finger != finger)
        {
            return null;
        }
        var end = moved ? new StrokeEnd(walk.ToArray(), false) : new StrokeEnd([cells[0]], true);
        Clear();
        return end;
    }

    // Focus lost or the app paused: the OS may never send the releases
    public void Reset()
    {
        down.Clear();
        Clear();
    }

    private void Clear()
    {
        finger = null;
        cells.Clear();
        crossed.Clear();
        walk.Clear();
    }
}
