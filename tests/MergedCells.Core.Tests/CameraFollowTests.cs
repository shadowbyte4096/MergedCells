using System.Numerics;

namespace MergedCells.Core.Tests;

public class CameraFollowTests
{
    private const float Square = 64;

    [Theory]
    [InlineData(1, 0)]
    [InlineData(-1, 0)]
    [InlineData(0, 1)]
    [InlineData(0, -1)]
    public void Player_stays_on_screen_during_a_long_slide(int columnStep, int rowStep)
    {
        var space = new CameraSpace(
            new BoardRect(0, 0, 100 * Square, 100 * Square), new Vector2(720, 1300), 24, Square, CameraSpaceTests.Config);
        var start = new Vector2(50 * Square + Square / 2, 50 * Square + Square / 2);
        var stop = start + new Vector2(columnStep, rowStep) * 40 * Square;
        var current = space.Centre(start, space.ReadableZoom);
        var glide = space.PlanFollow(current, current, space.ReadableZoom, start, stop);
        var slide = SlideTiming.Seconds(40, new SlideConfig { SquaresPerSecond = 15, MaxSeconds = 0.4 });

        Assert.NotNull(glide);
        Assert.NotNull(glide.HalfwayZoom);
        for (var frame = 0; frame / 60.0 <= glide.Delay + glide.Seconds; frame++)
        {
            var time = frame / 60.0;
            var player = Vector2.Lerp(start, stop, (float)Math.Min(time / slide, 1));
            Assert.True(space.ShowsSquare(space.Clamp(glide.Sample(time)), player), $"Player off screen at {time:0.000} s");
        }
    }
}
