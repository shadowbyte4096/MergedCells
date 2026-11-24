using System.Numerics;

namespace MergedCells.Core;

/// <summary>
/// Where the icon's circle sits on the first frame of the intro, in viewport units: exactly where the boot splash image
/// (icons/boot-splash.png) draws it, since Godot shows that image on every platform before the intro starts.
/// </summary>
public readonly record struct LaunchCircle(Vector2 Centre, float Diameter)
{
    // Android's own launch screen draws the icon in a 192 dp circle (no icon background). On a screen 411 dp wide (a
    // Pixel 10, and the emulator's Pixel 10 profile: 1080 pixels at 420 dpi, both measured) that is 336 units of the
    // 720-unit base, so all three pictures match. The boot splash scales with the screen instead (by its width on phones taller than 9:16, by its
    // height on wider screens), so elsewhere Android's own picture differs: on an 800 x 1280 dp tablet the boot splash's
    // circle is about 1.75 times Android's.
    public const float PhoneIconDp = 192;

    public const float BootSplashDiameter = 336;

    public static LaunchCircle For(Vector2 viewport) => new(viewport / 2, BootSplashDiameter);
}
