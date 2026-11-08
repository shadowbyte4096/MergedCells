using System.Numerics;

namespace MergedCells.Core;

/// <summary>
/// A timed camera move between two views, eased in and out, with an optional zoom-out that peaks halfway.
/// Samples aren't clamped: the caller clamps each one with the current <see cref="CameraSpace"/>.
/// </summary>
public sealed class CameraGlide
{
    /// <param name="halfwayZoom">The zoom at the halfway point. Ignored when it isn't below the zoom the glide
    /// would have there anyway, so a glide never zooms in past its ends.</param>
    /// <param name="seconds">How long the move takes, after the delay.</param>
    /// <param name="delay">Seconds spent on the from view first.</param>
    /// <param name="offsets">Planned offsets that keep the view on the board, in viewport units, at evenly spaced
    /// moments over <paramref name="seconds"/>; the first and last are zero. Null for none.</param>
    public CameraGlide(
        CameraView from, CameraView to, float? halfwayZoom, double seconds, double delay = 0, IReadOnlyList<Vector2>? offsets = null)
    {
        if (offsets is { Count: < 2 })
        {
            throw new ArgumentException("Needs at least two planned offsets, one at each end.", nameof(offsets));
        }
        RequirePositiveFinite(from.Zoom, nameof(from));
        RequirePositiveFinite(to.Zoom, nameof(to));
        if (halfwayZoom is { } halfway)
        {
            RequirePositiveFinite(halfway, nameof(halfwayZoom));
        }
        RequirePositiveFinite(seconds, nameof(seconds));
        if (!double.IsFinite(delay) || delay < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "Must be zero or more, and finite.");
        }

        From = from;
        To = to;
        HalfwayZoom = halfwayZoom;
        Seconds = seconds;
        Delay = delay;
        Offsets = offsets;
    }

    public CameraView From { get; }

    public CameraView To { get; }

    public float? HalfwayZoom { get; }

    public double Seconds { get; }

    public double Delay { get; }

    public IReadOnlyList<Vector2>? Offsets { get; }

    /// <summary>The view at this many seconds since the glide started, the delay included.</summary>
    public CameraView Sample(double elapsed)
    {
        var t = Math.Clamp((elapsed - Delay) / Seconds, 0, 1);
        if (t == 0)
        {
            return From;
        }
        if (t == 1)
        {
            return To;
        }

        var eased = t * t * (3 - 2 * t);
        var logFrom = Math.Log(From.Zoom);
        var logTo = Math.Log(To.Zoom);
        var logZoom = logFrom + (logTo - logFrom) * eased;
        if (HalfwayZoom is { } halfway)
        {
            var depth = Math.Log(halfway) - (logFrom + logTo) / 2;
            if (depth < 0)
            {
                logZoom += depth * Math.Sin(Math.PI * t);
            }
        }
        var zoom = (float)Math.Exp(logZoom);
        var focus = Vector2.Lerp(From.Focus, To.Focus, (float)eased);
        if (Offsets is { } offsets)
        {
            var position = t * (offsets.Count - 1);
            var index = Math.Min((int)position, offsets.Count - 2);
            focus += Vector2.Lerp(offsets[index], offsets[index + 1], (float)(position - index)) / zoom;
        }
        return new CameraView(focus, zoom);
    }

    public bool IsDone(double elapsed) => elapsed >= Delay + Seconds;

    private static void RequirePositiveFinite(double value, string name)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            throw new ArgumentOutOfRangeException(name, value, "Must be positive and finite.");
        }
    }
}
