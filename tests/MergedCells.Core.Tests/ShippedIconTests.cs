namespace MergedCells.Core.Tests;

public class ShippedIconTests
{
    public static TheoryData<string> IconSettings => new()
    {
        "config/icon",
        "boot_splash/image",
        "launcher_icons/main_192x192",
        "launcher_icons/adaptive_foreground_432x432",
        "launcher_icons/adaptive_background_432x432",
        "launcher_icons/adaptive_monochrome_432x432",
        "splash_screen/icon",
    };

    // The setting's value in project.godot or the Android preset, without its quotes
    private static string Setting(string name)
    {
        var lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "project.godot"))
            .Concat(File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "export_presets.cfg")));
        var line = lines.Single(text => text.StartsWith(name + "=", StringComparison.Ordinal));
        return line[(name.Length + 1)..].Trim('"');
    }

    [Theory]
    [MemberData(nameof(IconSettings))]
    public void Each_icon_setting_points_at_a_shipped_file(string name)
    {
        var value = Setting(name);

        Assert.StartsWith("res://", value);
        Assert.True(File.Exists(Path.Combine(AppContext.BaseDirectory, value["res://".Length..])), $"{name} points at {value}, which doesn't exist.");
    }
}
