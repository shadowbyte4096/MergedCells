using System.Numerics;

namespace MergedCells.Core;

// Scale: the new distance between the fingers over the previous one. Focal: their current midpoint.
public readonly record struct PinchStep(float Scale, Vector2 Focal);

/// <summary>
/// Follows the first two fingers down and turns their movement into pinch steps. A third finger is ignored,
/// and the pinch ends when either of its two fingers lifts. Positions are in viewport units.
/// </summary>
public sealed class PinchTracker
{
    private readonly List<(int Finger, Vector2 Position)> fingers = new(2);

    public bool IsPinching => fingers.Count == 2;

    /// <summary>A finger touched down. A finger that is already down is treated as lifting first.</summary>
    public void Press(int finger, float x, float y)
    {
        Release(finger);
        if (fingers.Count < 2)
        {
            fingers.Add((finger, new Vector2(x, y)));
        }
    }

    /// <summary>
    /// A finger moved. Returns a step when it's one of the two pinching fingers, or null otherwise,
    /// including when either distance between the fingers is zero.
    /// </summary>
    public PinchStep? Drag(int finger, float x, float y)
    {
        var index = fingers.FindIndex(tracked => tracked.Finger == finger);
        if (index < 0)
        {
            return null;
        }

        var before = IsPinching ? Vector2.Distance(fingers[0].Position, fingers[1].Position) : 0;
        fingers[index] = (finger, new Vector2(x, y));
        if (!IsPinching)
        {
            return null;
        }

        var after = Vector2.Distance(fingers[0].Position, fingers[1].Position);
        if (before == 0 || after == 0)
        {
            return null;
        }
        return new PinchStep(after / before, (fingers[0].Position + fingers[1].Position) / 2);
    }

    /// <summary>A finger lifted, or the OS cancelled its touch. Unknown fingers are ignored.</summary>
    public void Release(int finger) => fingers.RemoveAll(tracked => tracked.Finger == finger);

    /// <summary>Forgets every finger, for when releases may have been lost.</summary>
    public void Reset() => fingers.Clear();
}
