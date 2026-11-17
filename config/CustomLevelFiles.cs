using System;
using Godot;
using MergedCells.Core;

namespace MergedCells;

// The player's custom levels, one file each in user://custom-levels/. Like the save, nothing here ever stops the game: a
// file that can't be read is logged and left alone, and a write that fails is logged and shown on the list.
public static class CustomLevelFiles
{
    private const string FolderPath = "user://custom-levels";

    private static CustomLevelStore store;

    // Why the last change wasn't kept, for the list to show; null while writing works
    public static string Notice { get; private set; }

    private static CustomLevelStore Store => store ??= new CustomLevelStore(ProjectSettings.GlobalizePath(FolderPath));

    public static CustomLevelLoad LoadAll()
    {
        var load = Store.LoadAll();
        foreach (var skipped in load.Skipped)
        {
            GD.PushWarning($"Custom levels: skipped {skipped}");
        }
        return load;
    }

    public static void Write(CustomLevel level)
    {
        if (Store.Write(level) is { } problem)
        {
            GD.PushError($"Custom levels: can't write {level.Id:D}.json ({problem})");
            Notice = "This level couldn't be saved.";
        }
        else
        {
            Notice = null;
        }
    }

    public static void Delete(Guid id)
    {
        if (Store.Delete(id) is { } problem)
        {
            GD.PushError($"Custom levels: can't delete {id:D}.json ({problem})");
        }
    }
}
