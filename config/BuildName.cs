using Godot;

namespace MergedCells;

// The build's name on play records: the CI sets application/config/version to "playtest-<run>", and a store build sets its
// own; a build from the editor has none
public static class BuildName
{
    public static string Current
    {
        get
        {
            var version = ProjectSettings.GetSetting("application/config/version").AsString();
            return string.IsNullOrEmpty(version) ? "dev" : version;
        }
    }
}
