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

    // 61 samples from the start to the end
    private static IEnumerable<CameraView> Samples(CameraGlide glide) =>
        Enumerable.Range(0, 61).Select(i => glide.Sample(glide.Seconds * i / 60));
}
