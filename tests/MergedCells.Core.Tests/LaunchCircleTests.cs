using System.Numerics;
using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

public class LaunchCircleTests
{
    // One row per device: its density and window, and the launch screen's disc as screenshots measured it
    public static TheoryData<float, Vector2, float> MeasuredLaunchScreens => new()
    {
        // The MergedCells_API36 emulator (a Pixel 10 profile), after chunk 34b
        { 420, new Vector2(1080, 2424), 504 },
        // The developer's Pixel 10 (GrapheneOS, Android 17), after chunk 34b: its launch icon matched the emulator's pixel for pixel
        { 420, new Vector2(1080, 2424), 504 },
    };

    [Theory]
    [AutoData]
    public void The_circle_is_the_boot_splash_circle_at_the_centre(ushort width, ushort height)
    {
        var viewport = new Vector2(width + 1, height + 1);

        Assert.Equal(new LaunchCircle(viewport / 2, LaunchCircle.BootSplashDiameter), LaunchCircle.For(viewport));
    }

    // The base is 720 units across, so on a phone taller than 9:16 a unit is window.X / 720 pixels
    [Theory]
    [MemberData(nameof(MeasuredLaunchScreens))]
    public void The_circle_matches_the_measured_phone_launch_screen(float dpi, Vector2 window, float diameterPixels)
    {
        Assert.Equal(diameterPixels * 720 / window.X, LaunchCircle.BootSplashDiameter, 1f);
    }

    [Theory]
    [MemberData(nameof(MeasuredLaunchScreens))]
    public void The_phone_launch_icon_is_its_dp_size(float dpi, Vector2 window, float diameterPixels)
    {
        Assert.Equal(LaunchCircle.PhoneIconDp * dpi / 160, diameterPixels, 1f);
    }
}
