using System.Numerics;

namespace MergedCells.Core;

/// <summary>A title's place: its centre and width in viewport units, and its font size.</summary>
public readonly record struct TitleSpot(Vector2 Centre, float Width, float FontSize);

/// <summary>
/// One frame of the intro. Centres, radius, origin, scale and the title are in viewport units; ArtOrigin is where the
/// icon's point (0, 0) lands, and ArtScale is viewport units per icon unit. LeftBlockLeft, RightBlockRight and
/// TrailStart are the stretched ends, in icon units. TitleColourMix runs from the scene's ink (0) to the menu title's
/// colour (1).
/// </summary>
public readonly record struct IntroFrame(
    Vector2 CircleCentre,
    float CircleRadius,
    Vector2 ArtOrigin,
    float ArtScale,
    float LeftBlockLeft,
    float RightBlockRight,
    float TrailStart,
    float SceneAlpha,
    float BackgroundAlpha,
    TitleSpot Title,
    float TitleAlpha,
    float TitleColourMix,
    bool Done);

/// <summary>
/// The startup intro: from the launch screen's circle, the icon opens out to fill the screen, the title rises in,
/// holds, then the scene gives way to the menu while the title glides onto the menu's own.
/// </summary>
public sealed class IntroTimeline
{
    // Drawing details, in viewport units unless they say otherwise
    public const float OpenScale = 4;
    public const float OpenDrop = 20;
    public const float TitleRise = 30;
    public const float TitleAbove = 220;
    public const float SceneTitleSize = 76;
    public const float HandoffDrop = 40;
    public const float TrailPastEdge = 20;
    public const float LeftBlockPastEdge = 80;
    public const float RightBlockPastEdge = 40;

    // The background fades over the hand-off's last 60%, the scene over its first half
    private const double BackgroundOutStart = 0.4;

    private readonly IntroConfig times;
    private readonly IconArt art;
    private readonly LaunchCircle launch;

    public IntroTimeline(IntroConfig times, IconArt art, LaunchCircle launch)
    {
        ArgumentNullException.ThrowIfNull(times);
        ArgumentNullException.ThrowIfNull(art);
        times.CheckRanges();
        this.times = times;
        this.art = art;
        this.launch = launch;
        // A third of the title's time before the opening ends, but never before the intro starts
        TitleStart = Math.Max(0, times.OpenSeconds - times.TitleSeconds / 3);
        HoldStart = TitleStart + times.TitleSeconds;
        HandoffStart = HoldStart + times.HoldSeconds;
        TotalSeconds = HandoffStart + times.HandoffSeconds;
    }

    public double TitleStart { get; }

    public double HoldStart { get; }

    public double HandoffStart { get; }

    public double TotalSeconds { get; }

    public static TitleSpot SceneTitle(Vector2 viewport) =>
        new(new Vector2(viewport.X / 2, viewport.Y / 2 - TitleAbove), viewport.X, SceneTitleSize);

    public IntroFrame At(double seconds, Vector2 viewport, TitleSpot menuTitle)
    {
        var open = Eased(seconds, 0, times.OpenSeconds);
        var title = Eased(seconds, TitleStart, times.TitleSeconds);
        var handoff = Eased(seconds, HandoffStart, times.HandoffSeconds);
        var sceneOut = Eased(seconds, HandoffStart, times.HandoffSeconds / 2);
        var backgroundOut = Eased(seconds, HandoffStart + times.HandoffSeconds * BackgroundOutStart, times.HandoffSeconds * (1 - BackgroundOutStart));

        var openCentre = viewport / 2 + new Vector2(0, OpenDrop);
        var circleCentre = Vector2.Lerp(launch.Centre, openCentre, open);
        var scale = Lerp(launch.Diameter / 100, OpenScale, open);
        var origin = circleCentre + new Vector2(0, HandoffDrop * sceneOut) - new Vector2(50 * scale);
        var radius = Lerp(launch.Diameter / 2, FarthestCorner(openCentre, viewport) + 1, open);

        // Once open, the scene's ends sit past the viewport's edges, so no end ever shows on a wide or tall screen
        var openOriginX = openCentre.X - 50 * OpenScale;
        float IconX(float viewportX) => (viewportX - openOriginX) / OpenScale;
        var left = Lerp((float)art.LeftBlock.X, IconX(-LeftBlockPastEdge), open);
        var trail = Lerp((float)art.Player.TrailStart, IconX(-TrailPastEdge), open);
        var right = Lerp((float)(art.RightBlock.X + art.RightBlock.Width), IconX(viewport.X + RightBlockPastEdge), open);

        var scene = SceneTitle(viewport);
        var risen = scene.Centre + new Vector2(0, TitleRise * (1 - title));
        var spot = new TitleSpot(
            Vector2.Lerp(risen, menuTitle.Centre, handoff),
            Lerp(scene.Width, menuTitle.Width, handoff),
            Lerp(scene.FontSize, menuTitle.FontSize, handoff));

        return new IntroFrame(
            circleCentre, radius, origin, scale, left, right, trail, 1 - sceneOut, 1 - backgroundOut, spot, title, handoff,
            seconds >= TotalSeconds);
    }

    // How far a part has got, eased in and out (cubic), from 0 before it starts to 1 once it has run its length
    private static float Eased(double seconds, double start, double length)
    {
        var x = Math.Clamp((seconds - start) / length, 0, 1);
        return (float)(x < 0.5 ? 4 * x * x * x : 1 - Math.Pow(-2 * x + 2, 3) / 2);
    }

    private static float Lerp(float from, float to, float amount) => from + (to - from) * amount;

    private static float FarthestCorner(Vector2 point, Vector2 viewport) =>
        new[] { Vector2.Zero, new Vector2(viewport.X, 0), new Vector2(0, viewport.Y), viewport }
            .Max(corner => Vector2.Distance(point, corner));
}
