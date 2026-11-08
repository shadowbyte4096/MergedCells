using Godot;
using MergedCells.Core;

namespace MergedCells;

// The player's save, loaded once on first use. Unlike the config, nothing here ever stops the game: SaveStore sets a
// broken file aside, and leaves a newer or unreadable one alone and writes nothing.
public static class SaveGameFile
{
    private const string FilePath = "user://save.json";

    private static SaveStore store;
    private static SaveGame current;

    public static SaveGame Current => current ??= Load();

    // Straight away on every change, so Android killing the app loses nothing
    public static void Save()
    {
        if (current == null || !store.CanWrite)
        {
            return;
        }
        if (store.Write(current) is { } problem)
        {
            GD.PushError($"Save: can't write {FilePath} ({problem})");
        }
    }

    private static SaveGame Load()
    {
        store = new SaveStore(ProjectSettings.GlobalizePath(FilePath));
        var load = store.Load();
        switch (load.Outcome)
        {
            case SaveOutcome.Missing:
                GD.Print("Save: none yet, starting fresh");
                break;
            case SaveOutcome.Loaded:
                GD.Print($"Save: loaded {load.Save.LevelCount} {(load.Save.LevelCount == 1 ? "level" : "levels")}");
                break;
            case SaveOutcome.Broken:
                GD.PushError($"Save: {FilePath} is broken ({load.Problem}); kept as save.broken.json, starting fresh");
                break;
            case SaveOutcome.Newer:
                GD.PushWarning($"Save: {FilePath} is from a newer version ({load.Problem}); playing without saving");
                break;
            default:
                GD.PushError($"Save: can't read {FilePath} ({load.Problem}); playing without saving");
                break;
        }
        return load.Save;
    }
}
