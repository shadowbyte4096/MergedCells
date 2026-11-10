using Godot;
using MergedCells.Core;

namespace MergedCells;

// The player's save, loaded once on first use. Unlike the config, nothing here ever stops the game: SaveStore sets a
// broken file aside, and leaves a newer or unreadable one alone and writes nothing.
public static class SaveGameFile
{
    private const string FilePath = "user://save.json";

    // A backup or antivirus tool can hold the file open for a moment; without another try the whole run goes unsaved
    private const int UnreadableRetries = 2;
    private const int RetryDelayMilliseconds = 100;

    private static SaveStore store;
    private static SaveGame current;

    // Why this run's progress isn't being kept, for the mode select to show; null while saving works
    public static string Notice { get; private set; }

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
            Notice = "The save file can't be written, so progress isn't being saved.";
        }
        else
        {
            Notice = null;
        }
    }

    private static SaveGame Load()
    {
        store = new SaveStore(ProjectSettings.GlobalizePath(FilePath));
        var load = store.Load();
        for (var retry = 0; retry < UnreadableRetries && load.Outcome == SaveOutcome.Unreadable; retry++)
        {
            System.Threading.Thread.Sleep(RetryDelayMilliseconds);
            load = store.Load();
        }
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
                Notice = "This save is from a newer version of the game, so progress won't be saved.";
                break;
            default:
                GD.PushError($"Save: can't read {FilePath} ({load.Problem}); playing without saving");
                Notice = "The save file can't be read, so progress won't be saved.";
                break;
        }
        return load.Save;
    }
}
