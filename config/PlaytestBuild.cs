using Godot;

namespace MergedCells;

// Playtest builds are the CI's exports (the "playtest" feature) and every debug build; store builds are neither
public static class PlaytestBuild
{
    public static bool IsOn => OS.IsDebugBuild() || OS.HasFeature("playtest");

    // The CI sets application/config/version to "playtest-<run>"; a build from the editor has none
    public static string Build
    {
        get
        {
            var version = ProjectSettings.GetSetting("application/config/version").AsString();
            return string.IsNullOrEmpty(version) ? "dev" : version;
        }
    }
}
