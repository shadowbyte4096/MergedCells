using System.Numerics;

namespace MergedCells.Core;

/// <summary>
/// Follows a mouse drag and turns its movement into pan steps once the pointer has moved past a dead zone from where
/// the button went down. Positions are in viewport units.
/// </summary>
public sealed class DragTracker
{
    private readonly float slop;
    private Vector2 anchor;
    private Vector2 position;
    private bool moved;

    /// <param name="slop">How far the pointer must move from the press, in viewport units, before the drag gives
    /// steps.</param>
    public DragTracker(float slop)
    {
        if (!float.IsFinite(slop) || slop < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(slop), slop, "Must be zero or more, and finite.");
        }
        this.slop = slop;
    }

    public bool IsDown { get; private set; }

    public void Press(float x, float y)
    {
        anchor = position = new Vector2(x, y);
        moved = false;
        IsDown = true;
    }

    /// <summary>The pointer moved. Returns a step once the drag is past the dead zone, or null otherwise.</summary>
    public GestureStep? Move(float x, float y)
    {
        if (!IsDown)
        {
            return null;
        }
        var before = position;
        position = new Vector2(x, y);
        moved = moved || Vector2.Distance(position, anchor) > slop;
        return moved ? new GestureStep(1, before, position) : null;
    }

    public void Release() => IsDown = false;
}
