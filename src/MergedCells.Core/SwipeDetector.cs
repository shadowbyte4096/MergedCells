namespace MergedCells.Core;

/// <summary>
/// Turns touches into moves: one move per gesture, fired when a single finger has travelled
/// the threshold along either axis from where it touched down. A gesture runs from the first
/// finger down to the last finger up, and a gesture that ever has two fingers down never fires,
/// because two-finger gestures belong to the camera.
/// </summary>
public sealed class SwipeDetector
{
    private readonly float threshold;
    private readonly Dictionary<int, (float X, float Y)> touchDowns = new();

    // Set once the gesture has fired or gained a second finger; cleared when the last finger lifts
    private bool spent;

    /// <param name="threshold">Distance along one axis, in the same units as the touch coordinates.</param>
    public SwipeDetector(float threshold)
    {
        if (!float.IsFinite(threshold) || threshold <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(threshold), threshold, "The swipe threshold must be positive and finite.");
        }
        this.threshold = threshold;
    }

    /// <summary>
    /// A finger touched down. A finger that is already down is treated as lifting first, because its release was lost.
    /// </summary>
    public void Press(int finger, float x, float y)
    {
        Release(finger);
        touchDowns[finger] = (x, y);
        if (touchDowns.Count > 1)
        {
            spent = true;
        }
    }

    /// <summary>
    /// A finger moved. Returns the swipe's direction when this drag completes one; otherwise null.
    /// The larger axis decides, +x is right and +y is down; an exact diagonal waits for a later drag.
    /// </summary>
    public Direction? Drag(int finger, float x, float y)
    {
        if (spent || !touchDowns.TryGetValue(finger, out var touchDown))
        {
            return null;
        }

        var dx = x - touchDown.X;
        var dy = y - touchDown.Y;
        var distanceX = Math.Abs(dx);
        var distanceY = Math.Abs(dy);
        if (Math.Max(distanceX, distanceY) < threshold || distanceX == distanceY)
        {
            return null;
        }

        spent = true;
        if (distanceX > distanceY)
        {
            return dx > 0 ? Direction.Right : Direction.Left;
        }
        return dy > 0 ? Direction.Down : Direction.Up;
    }

    /// <summary>A finger lifted, or the OS cancelled its touch. Unknown fingers are ignored.</summary>
    public void Release(int finger)
    {
        if (touchDowns.Remove(finger) && touchDowns.Count == 0)
        {
            spent = false;
        }
    }

    /// <summary>Forgets every finger, for when releases may have been lost (the app lost focus or was paused).</summary>
    public void Reset()
    {
        touchDowns.Clear();
        spent = false;
    }
}
