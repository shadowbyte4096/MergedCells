using System.Numerics;
using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

// AutoFixture's numbers are positive whole numbers, so generated zooms and times are valid
public class CameraGlideTests
{
    [Theory]
    [AutoData]
    public void Starts_on_its_from_view(CameraView start, CameraView end, double seconds)
    {
        Assert.Equal(start, new CameraGlide(start, end, null, seconds).Sample(0));
    }

    [Theory]
    [InlineAutoData(0)]
    [InlineAutoData(1)]
    public void Ends_on_its_to_view(double after, CameraView start, CameraView end, double seconds)
    {
        Assert.Equal(end, new CameraGlide(start, end, null, seconds).Sample(seconds + after));
    }

    [Theory]
    [InlineAutoData(0.5)]
    [InlineAutoData(1)]
    public void Holds_its_from_view_during_the_delay(
        double delayFraction, CameraView start, CameraView end, double seconds, double delay)
    {
        var glide = new CameraGlide(start, end, null, seconds, delay);

        Assert.Equal(start, glide.Sample(delay * delayFraction));
    }

    [Theory]
    [AutoData]
    public void Halfway_focus_is_the_midpoint(CameraView start, CameraView end, double seconds)
    {
        var glide = new CameraGlide(start, end, null, seconds);

        Assert.Equal(Vector2.Lerp(start.Focus, end.Focus, 0.5f), glide.Sample(seconds / 2).Focus);
    }

    [Theory]
    [AutoData]
    public void Halfway_zoom_is_reached_at_the_halfway_point(CameraView start, CameraView end, double seconds)
    {
        var glide = new CameraGlide(start, end with { Zoom = start.Zoom }, start.Zoom / 2, seconds);

        Assert.Equal(start.Zoom / 2, glide.Sample(seconds / 2).Zoom, start.Zoom * 1e-4f);
    }

    [Theory]
    [AutoData]
    public void Halfway_zoom_above_the_base_is_ignored(CameraView start, CameraView end, double seconds)
    {
        var glide = new CameraGlide(start, end with { Zoom = start.Zoom }, start.Zoom * 2, seconds);

        Assert.All(Samples(glide), view => Assert.Equal(start.Zoom, view.Zoom, start.Zoom * 1e-4f));
    }

    [Theory]
    [AutoData]
    public void Without_a_halfway_zoom_the_zoom_stays_between_its_ends(CameraView start, CameraView end, double seconds)
    {
        var glide = new CameraGlide(start, end with { Zoom = start.Zoom * 2 }, null, seconds);

        Assert.All(Samples(glide), view => Assert.InRange(view.Zoom, start.Zoom, start.Zoom * 2));
    }

    [Theory]
    [AutoData]
    public void IsDone_turns_true_at_the_end(CameraView start, CameraView end, double seconds, double delay)
    {
        var glide = new CameraGlide(start, end, null, seconds, delay);

        Assert.Equal((false, true), (glide.IsDone(delay + seconds - 0.001), glide.IsDone(delay + seconds)));
    }

    // The invalid values themselves are what's tested, so they're literals
    [Theory]
    [InlineAutoData(0, 0, 1, 1, 1)]
    [InlineAutoData(-1, 0, 1, 1, 1)]
    [InlineAutoData(double.NaN, 0, 1, 1, 1)]
    [InlineAutoData(double.PositiveInfinity, 0, 1, 1, 1)]
    [InlineAutoData(0.5, -1, 1, 1, 1)]
    [InlineAutoData(0.5, double.NaN, 1, 1, 1)]
    [InlineAutoData(0.5, 0, 0, 1, 1)]
    [InlineAutoData(0.5, 0, 1, 0, 1)]
    [InlineAutoData(0.5, 0, 1, 1, 0)]
    public void Invalid_times_or_zooms_throw(
        double seconds, double delay, float fromZoom, float toZoom, float halfway, CameraView start, CameraView end)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CameraGlide(start with { Zoom = fromZoom }, end with { Zoom = toZoom }, halfway, seconds, delay));
    }

    [Theory]
    [AutoData]
    public void Planned_offsets_leave_the_start_and_end_views_alone(CameraView start, CameraView end, double seconds)
    {
        var glide = new CameraGlide(start, end, null, seconds, offsets: [Vector2.Zero, new Vector2(50, 20), Vector2.Zero]);

        Assert.Equal((start, end), (glide.Sample(0), glide.Sample(seconds)));
    }

    // Offsets are viewport units, so at zoom 2 an offset of 64 moves the focus 32 board units
    [Theory]
    [AutoData]
    public void Planned_offset_is_added_over_the_zoom(Vector2 from, Vector2 to, double seconds)
    {
        var glide = new CameraGlide(
            new CameraView(from, 2), new CameraView(to, 2), null, seconds, offsets: [Vector2.Zero, new Vector2(64, 0), Vector2.Zero]);

        var focus = glide.Sample(seconds / 2).Focus;

        var expected = Vector2.Lerp(from, to, 0.5f) + new Vector2(32, 0);
        Assert.Equal(expected.X, focus.X, 0.001f);
        Assert.Equal(expected.Y, focus.Y, 0.001f);
    }

    // Five offsets are four intervals: each fraction falls between two moments, or on one, after the delay
    [Theory]
    [InlineAutoData(0.25, 100)]
    [InlineAutoData(0.375, 150)]
    [InlineAutoData(0.625, 130)]
    public void Planned_offsets_are_read_between_their_own_two_moments_after_the_delay(
        double fraction, float expected, Vector2 focus, double seconds, double delay)
    {
        var view = new CameraView(focus, 1);
        var glide = new CameraGlide(
            view, view, null, seconds, delay, [Vector2.Zero, new Vector2(100, 0), new Vector2(200, 0), new Vector2(60, 0), Vector2.Zero]);

        Assert.Equal(focus.X + expected, glide.Sample(delay + seconds * fraction).Focus.X, 0.01f);
    }

    [Theory]
    [AutoData]
    public void Fewer_than_two_offsets_throws(CameraView start, CameraView end, double seconds)
    {
        Assert.Throws<ArgumentException>(() => new CameraGlide(start, end, null, seconds, offsets: [Vector2.Zero]));
    }

    // 61 samples from the start to the end
    private static IEnumerable<CameraView> Samples(CameraGlide glide) =>
        Enumerable.Range(0, 61).Select(i => glide.Sample(glide.Seconds * i / 60));
}
