using System.Numerics;

namespace MergedCells.Core;

/// <summary>
/// The space the camera works in: the board, the band between the HUD's bars, and the camera config.
/// It holds the zoom limits and keeps views on the board. Immutable: build a new one when anything changes.
/// </summary>
public sealed class CameraSpace
{
    // How finely a glide's planned offsets are sampled and solved: algorithm details, not tuning numbers
    private const int OffsetIntervals = 64;
    private const int SolverSweeps = 150;
    private static readonly int[] SolverLevels = [16, 32, OffsetIntervals];

    private readonly BoardRect board;
    private readonly Vector2 band;
    private readonly float margin;
    private readonly float squareSize;
    private readonly CameraConfig config;

    /// <param name="board">The board's bounds, in board units.</param>
    /// <param name="band">The band's size, in viewport units. Each axis must be more than twice the margin.</param>
    /// <param name="margin">Viewport units kept free round the whole map.</param>
    /// <param name="squareSize">Board units per square.</param>
    public CameraSpace(BoardRect board, Vector2 band, float margin, float squareSize, CameraConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        RequirePositiveFinite(config.ReadableSquareSize, nameof(config.ReadableSquareSize));
        RequirePositiveFinite(config.ClosestSquareSize, nameof(config.ClosestSquareSize));
        if (!float.IsFinite(config.FollowZonePercent) || config.FollowZonePercent <= 0 || config.FollowZonePercent > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(config.FollowZonePercent), config.FollowZonePercent, "Must be above 0 and at most 100.");
        }
        RequirePositiveFinite(config.GlideSeconds, nameof(config.GlideSeconds));
        RequirePositiveFinite(config.OpeningSeconds, nameof(config.OpeningSeconds));
        if (!double.IsFinite(config.OpeningHoldSeconds) || config.OpeningHoldSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(config.OpeningHoldSeconds), config.OpeningHoldSeconds, "Must be zero or more, and finite.");
        }
        if (!float.IsFinite(config.ZoomStep) || config.ZoomStep <= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(config.ZoomStep), config.ZoomStep, "Must be above 1, and finite.");
        }
        RequirePositiveFinite(board.Width, nameof(board));
        RequirePositiveFinite(board.Height, nameof(board));
        RequirePositiveFinite(squareSize, nameof(squareSize));
        if (!float.IsFinite(margin) || margin < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(margin), margin, "Must be zero or more, and finite.");
        }
        if (!(band.X > 2 * margin) || !(band.Y > 2 * margin) || !float.IsFinite(band.X) || !float.IsFinite(band.Y))
        {
            throw new ArgumentOutOfRangeException(nameof(band), band, "Each axis must be finite and more than twice the margin.");
        }

        this.board = board;
        this.band = band;
        this.margin = margin;
        this.squareSize = squareSize;
        this.config = config;

        WholeZoom = Math.Min((band.X - 2 * margin) / board.Width, (band.Y - 2 * margin) / board.Height);
        ClosestZoom = Math.Max(WholeZoom, config.ClosestSquareSize / squareSize);
        ReadableZoom = Math.Clamp(config.ReadableSquareSize / squareSize, WholeZoom, ClosestZoom);
        Whole = new CameraView(board.Centre, WholeZoom);
    }

    /// <summary>The zoom that fits the whole board in the band, with the margin on every side.</summary>
    public float WholeZoom { get; }

    public float ClosestZoom { get; }

    /// <summary>The zoom a big level opens to: the readable square size, clamped to the limits.</summary>
    public float ReadableZoom { get; }

    /// <summary>True when the whole map is already at least as close as the readable zoom.</summary>
    public bool OpensWhole => ReadableZoom <= WholeZoom;

    public CameraView Whole { get; }

    /// <summary>The view with its zoom within the limits, moved so it shows no more than the margin past the board.</summary>
    public CameraView Clamp(CameraView view)
    {
        var zoom = Math.Clamp(view.Zoom, WholeZoom, ClosestZoom);
        return new CameraView(
            new Vector2(
                ClampAxis(view.Focus.X, board.Left, board.Right, band.X, zoom),
                ClampAxis(view.Focus.Y, board.Top, board.Bottom, band.Y, zoom)),
            zoom);
    }

    public CameraView Centre(Vector2 point, float zoom) => Clamp(new CameraView(point, zoom));

    /// <summary>
    /// A view carried over from the space before a resize or a change in the bars: a whole-map view stays the whole map,
    /// and any other view is clamped.
    /// </summary>
    public CameraView Resized(CameraView view, CameraSpace? previous) =>
        previous is not null && view.Zoom <= previous.WholeZoom * (1 + 1e-4f) ? Whole : Clamp(view);

    /// <summary>
    /// Zooms by a factor about a point in the band (viewport units from its top-left), keeping the board point under it.
    /// </summary>
    public CameraView ZoomAt(CameraView view, float factor, Vector2 bandPoint) => PanZoom(view, factor, bandPoint, bandPoint);

    /// <summary>
    /// Moves and zooms the view so the board point under <paramref name="bandFrom"/> ends up under
    /// <paramref name="bandTo"/>, with the zoom scaled by the factor, then clamps it. Band points are viewport units from
    /// the band's top-left.
    /// </summary>
    public CameraView PanZoom(CameraView view, float factor, Vector2 bandFrom, Vector2 bandTo)
    {
        var anchor = view.Focus + (bandFrom - band / 2) / view.Zoom;
        var zoom = Math.Clamp(view.Zoom * factor, WholeZoom, ClosestZoom);
        return Clamp(new CameraView(anchor - (bandTo - band / 2) / zoom, zoom));
    }

    /// <summary>The zoom factor for this many wheel notches or key presses: negative zooms out.</summary>
    public float ZoomFactor(float notches) => MathF.Pow(config.ZoomStep, notches);

    /// <summary>Whether a board point is inside the view's middle area, the follow zone, on both axes.</summary>
    public bool InFollowZone(CameraView view, Vector2 point)
    {
        var half = band * (config.FollowZonePercent / 100) / 2 / view.Zoom;
        var distance = Vector2.Abs(point - view.Focus);
        return distance.X <= half.X && distance.Y <= half.Y;
    }

    /// <summary>Whether the whole square centred on this board point is inside the band.</summary>
    public bool ShowsSquare(CameraView view, Vector2 squareCentre)
    {
        var half = band / 2 / view.Zoom;
        var reach = Vector2.Abs(squareCentre - view.Focus) + new Vector2(squareSize / 2);
        return reach.X <= half.X && reach.Y <= half.Y;
    }

    /// <summary>
    /// The glide for a slide from one square centre to another, or null when the camera should leave things as they are.
    /// </summary>
    /// <param name="current">Where the camera is now: the glide starts here.</param>
    /// <param name="heading">The end of the glide in progress, or <paramref name="current"/> when none is running.
    /// The stop is judged against this view.</param>
    /// <param name="playerZoom">The zoom the player chose, which the glide ends on.</param>
    /// <param name="slideSeconds">How long the player's slide takes, so the plan can keep their square on screen.</param>
    public CameraGlide? PlanFollow(
        CameraView current, CameraView heading, float playerZoom, Vector2 from, Vector2 to, double slideSeconds)
    {
        if (Target(heading, playerZoom, to) is not { } target)
        {
            return null;
        }
        float? halfway = ShowsSquare(heading, to) ? null : HalfwayZoom(current, target, from, to);
        return WithOffsets(
            new CameraGlide(current, target, halfway, config.GlideSeconds),
            time => slideSeconds > 0 ? Vector2.Lerp(from, to, (float)Math.Min(time / slideSeconds, 1)) : to);
    }

    /// <summary>
    /// As <see cref="PlanFollow"/>, for a player who snapped to a square (undo, restart). The zoom-out fits the current
    /// view's middle and the player's square.
    /// </summary>
    public CameraGlide? PlanRecentre(CameraView current, CameraView heading, float playerZoom, Vector2 at)
    {
        if (Target(heading, playerZoom, at) is not { } target)
        {
            return null;
        }
        float? halfway = ShowsSquare(heading, at) ? null : HalfwayZoom(current, target, current.Focus, at);
        return WithOffsets(new CameraGlide(current, target, halfway, config.GlideSeconds), _ => at);
    }

    /// <summary>From the whole map to the readable zoom centred on the start, after the config's hold.</summary>
    public CameraGlide PlanOpening(Vector2 start) =>
        new(Whole, Centre(start, ReadableZoom), null, config.OpeningSeconds, config.OpeningHoldSeconds);

    // Null when the point is in the heading view's middle area, or the camera is already heading for the clamped target
    private CameraView? Target(CameraView heading, float playerZoom, Vector2 point)
    {
        if (InFollowZone(heading, point))
        {
            return null;
        }
        var target = Centre(point, playerZoom);
        var moved = Vector2.Abs(target.Focus - heading.Focus);
        if (moved.X < 0.01f && moved.Y < 0.01f && Math.Abs(target.Zoom - heading.Zoom) <= heading.Zoom * 1e-4f)
        {
            return null;
        }
        return target;
    }

    // The closest zoom at which both squares fit in the band, with the margin, round the glide's halfway position
    private float HalfwayZoom(CameraView current, CameraView target, Vector2 from, Vector2 to)
    {
        var middle = (current.Focus + target.Focus) / 2;
        var extent = Vector2.Max(Vector2.Abs(from - middle), Vector2.Abs(to - middle)) + new Vector2(squareSize / 2);
        var fit = Math.Min((band.X - 2 * margin) / (2 * extent.X), (band.Y - 2 * margin) / (2 * extent.Y));
        return Math.Max(WholeZoom, Math.Min(fit, Math.Min(current.Zoom, target.Zoom)));
    }

    // A glide's path is planned round the board's edges, so the per-frame clamp never shoves the view mid-glide.
    // Unchanged when no moment of the glide needs the clamp.
    private CameraGlide WithOffsets(CameraGlide glide, Func<double, Vector2> player) =>
        PlanOffsets(glide, player) is { } offsets
            ? new CameraGlide(glide.From, glide.To, glide.HalfwayZoom, glide.Seconds, glide.Delay, offsets)
            : glide;

    // For each axis: the offset from chunk 8's path, in viewport units, that keeps the view on the board at each moment,
    // with the least change in speed. Null when no moment needs one
    private Vector2[]? PlanOffsets(CameraGlide glide, Func<double, Vector2> player)
    {
        var lowX = new double[OffsetIntervals + 1];
        var highX = new double[OffsetIntervals + 1];
        var lowY = new double[OffsetIntervals + 1];
        var highY = new double[OffsetIntervals + 1];
        var needed = false;
        for (var i = 0; i <= OffsetIntervals; i++)
        {
            var time = glide.Delay + glide.Seconds * i / OffsetIntervals;
            var view = glide.Sample(time);
            var at = player(time);
            needed |= Allowed(view.Focus.X, view.Zoom, board.Left, board.Right, band.X, at.X, out lowX[i], out highX[i]);
            needed |= Allowed(view.Focus.Y, view.Zoom, board.Top, board.Bottom, band.Y, at.Y, out lowY[i], out highY[i]);
        }
        if (!needed)
        {
            return null;
        }

        var x = Smoothest(lowX, highX);
        var y = Smoothest(lowY, highY);
        var offsets = new Vector2[OffsetIntervals + 1];
        for (var i = 0; i <= OffsetIntervals; i++)
        {
            offsets[i] = new Vector2((float)x[i], (float)y[i]);
        }
        return offsets;
    }

    // The offsets on one axis, in viewport units, that keep the view on the board (Clamp's rule, centring an axis wider
    // than the board), narrowed to keep the player's square on screen where both can hold. True when the board's range
    // excludes no offset, so the clamp would move this moment
    private bool Allowed(float focus, float zoom, float start, float end, float bandSize, float player, out double low, out double high)
    {
        var inset = (bandSize / 2 - margin) / (double)zoom;
        var centre = (start + end) / 2.0;
        low = (Math.Min(start + inset, centre) - focus) * zoom;
        high = (Math.Max(end - inset, centre) - focus) * zoom;
        var needed = low > 1e-3 || high < -1e-3;

        var reach = bandSize / 2 - squareSize / 2 * zoom;
        var relative = (player - focus) * (double)zoom;
        var playerLow = Math.Max(low, relative - reach);
        var playerHigh = Math.Min(high, relative + reach);
        if (playerLow <= playerHigh)
        {
            low = playerLow;
            high = playerHigh;
        }
        return needed;
    }

    // Minimises the squared second differences (the change in speed) inside the ranges, with both ends at zero:
    // coarse to fine, each level starting from the one before, by Gauss-Seidel sweeps clamped into the ranges
    private static double[] Smoothest(double[] low, double[] high)
    {
        double[]? previous = null;
        foreach (var intervals in SolverLevels)
        {
            var step = OffsetIntervals / intervals;
            var offsets = new double[intervals + 1];
            for (var i = 1; i < intervals; i++)
            {
                var guess = previous is null ? 0 : i % 2 == 0 ? previous[i / 2] : (previous[i / 2] + previous[i / 2 + 1]) / 2;
                offsets[i] = Math.Clamp(guess, low[i * step], high[i * step]);
            }
            for (var sweep = 0; sweep < SolverSweeps; sweep++)
            {
                for (var i = 1; i < intervals; i++)
                {
                    offsets[i] = Math.Clamp(Relaxed(offsets, i), low[i * step], high[i * step]);
                }
            }
            previous = offsets;
        }
        return previous!;
    }

    // The value at i that makes the squared second differences round it least, the others held. The view is still
    // before and after the glide, so past each end the offsets mirror evenly (zero slope) and the end's own second
    // difference counts at half weight; next to an end that leaves 7 on the diagonal instead of 6. Mirroring oddly
    // instead pinned the ends at zero but let the offsets start and stop at full speed.
    private static double Relaxed(double[] offsets, int i)
    {
        var last = offsets.Length - 1;
        if (last < 3)
        {
            return 0;
        }
        if (i == 1)
        {
            return (4 * offsets[2] - offsets[3]) / 7;
        }
        if (i == last - 1)
        {
            return (4 * offsets[last - 2] - offsets[last - 3]) / 7;
        }
        return -(offsets[i - 2] - 4 * offsets[i - 1] - 4 * offsets[i + 1] + offsets[i + 2]) / 6;
    }

    private float ClampAxis(float focus, float start, float end, float bandSize, float zoom)
    {
        var half = bandSize / 2 / zoom;
        var slack = margin / zoom;
        var low = start - slack + half;
        var high = end + slack - half;
        // At the whole-map zoom the two meet, and float error can cross them, which Math.Clamp would throw on
        if (low >= high)
        {
            return (start + end) / 2;
        }
        return Math.Clamp(focus, low, high);
    }

    private static void RequirePositiveFinite(double value, string name)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            throw new ArgumentOutOfRangeException(name, value, "Must be positive and finite.");
        }
    }
}
