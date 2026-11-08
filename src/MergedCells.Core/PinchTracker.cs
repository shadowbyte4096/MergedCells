using System.Numerics;

namespace MergedCells.Core;

// Scale: the new distance between the fingers over the previous one (1 for a drag). From and To: the gesture's point
// (the fingers' midpoint, or the pointer) before and after the step, in viewport units
public readonly record struct GestureStep(float Scale, Vector2 From, Vector2 To)
{
    /// <summary>
    /// This step followed by the next one, as one step. Each finger's drag is its own step, so a camera that applies a
    /// frame's steps one by one zooms out and back in, and the clamp at the board's edge creeps the view inward.
    /// </summary>
    public GestureStep Then(GestureStep next) => new(Scale * next.Scale, From, next.To);
}

/// <summary>
/// Follows the first two fingers down and turns their movement into one gesture step per frame, once the gesture has
/// moved past a dead zone. A third finger is ignored, and the gesture ends when either of its two fingers lifts.
/// Positions are in viewport units.
/// </summary>
/// <remarks>
/// Steps span whole frames because each finger's drag arrives as its own event: a step per event would zoom out and
/// back in within a frame, which the clamp at the board's edge turns into a creep, and a dead zone crossed between two
/// fingers' events would apply half of that wobble.
/// </remarks>
public sealed class PinchTracker
{
    private readonly float slop;
    private readonly List<(int Finger, Vector2 Position)> fingers = new(2);
    private Vector2 anchorMidpoint;
    private float anchorDistance;
    private Vector2 takenMidpoint;
    private float takenDistance;
    private bool moved;

    /// <param name="slop">How far the midpoint must move, or the distance between the fingers change, in viewport
    /// units, before the gesture gives steps.</param>
    public PinchTracker(float slop)
    {
        if (!float.IsFinite(slop) || slop < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(slop), slop, "Must be zero or more, and finite.");
        }
        this.slop = slop;
    }

    public bool IsPinching => fingers.Count == 2;

    /// <summary>A finger touched down. A finger that is already down is treated as lifting first.</summary>
    public void Press(int finger, float x, float y)
    {
        Release(finger);
        if (fingers.Count >= 2)
        {
            return;
        }
        fingers.Add((finger, new Vector2(x, y)));
        if (IsPinching)
        {
            anchorMidpoint = takenMidpoint = Midpoint;
            anchorDistance = takenDistance = Distance;
            moved = false;
        }
    }

    /// <summary>A finger moved. Fingers that aren't one of the two pinching ones are ignored.</summary>
    public void Drag(int finger, float x, float y)
    {
        var index = fingers.FindIndex(tracked => tracked.Finger == finger);
        if (index >= 0)
        {
            fingers[index] = (finger, new Vector2(x, y));
        }
    }

    /// <summary>
    /// The pinch's movement since the last call, as one step, for calling once a frame. Null when not pinching, inside
    /// the dead zone, when nothing moved, or when either distance between the fingers is zero. The first step past the
    /// dead zone starts from where the fingers were at the last call, so the dead zone's movement is never applied.
    /// </summary>
    public GestureStep? TakeStep()
    {
        if (!IsPinching)
        {
            return null;
        }
        var midpoint = Midpoint;
        var distance = Distance;
        var fromMidpoint = takenMidpoint;
        var fromDistance = takenDistance;
        takenMidpoint = midpoint;
        takenDistance = distance;

        moved = moved || Vector2.Distance(midpoint, anchorMidpoint) > slop || Math.Abs(distance - anchorDistance) > slop;
        if (!moved || fromDistance == 0 || distance == 0 || (midpoint == fromMidpoint && distance == fromDistance))
        {
            return null;
        }
        return new GestureStep(distance / fromDistance, fromMidpoint, midpoint);
    }

    /// <summary>A finger lifted, or the OS cancelled its touch. Unknown fingers are ignored.</summary>
    public void Release(int finger) => fingers.RemoveAll(tracked => tracked.Finger == finger);

    /// <summary>Forgets every finger, for when releases may have been lost.</summary>
    public void Reset() => fingers.Clear();

    private Vector2 Midpoint => (fingers[0].Position + fingers[1].Position) / 2;

    private float Distance => Vector2.Distance(fingers[0].Position, fingers[1].Position);
}
