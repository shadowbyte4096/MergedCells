using System.Numerics;

namespace MergedCells.Core;

/// <summary>
/// The space the camera works in: the board, the band between the HUD's bars, and the camera config.
/// It holds the zoom limits and keeps views on the board. Immutable: build a new one when anything changes.
/// </summary>
public sealed class CameraSpace
{
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
    public CameraView ZoomAt(CameraView view, float factor, Vector2 bandPoint)
    {
        var offset = bandPoint - band / 2;
        var anchor = view.Focus + offset / view.Zoom;
        var zoom = Math.Clamp(view.Zoom * factor, WholeZoom, ClosestZoom);
        return Clamp(new CameraView(anchor - offset / zoom, zoom));
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
    public CameraGlide? PlanFollow(CameraView current, CameraView heading, float playerZoom, Vector2 from, Vector2 to)
    {
        if (Target(heading, playerZoom, to) is not { } target)
        {
            return null;
        }
        float? halfway = ShowsSquare(heading, to) ? null : HalfwayZoom(current, target, from, to);
        return new CameraGlide(current, target, halfway, config.GlideSeconds);
    }

    /// <summary>As <see cref="PlanFollow"/>, for a player who snapped to a square (undo, restart), with no zoom-out.</summary>
    public CameraGlide? PlanRecentre(CameraView current, CameraView heading, float playerZoom, Vector2 at) =>
        Target(heading, playerZoom, at) is { } target ? new CameraGlide(current, target, null, config.GlideSeconds) : null;

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
