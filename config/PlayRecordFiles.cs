using System;
using System.Globalization;
using Godot;
using MergedCells.Core;

namespace MergedCells;

// The play records file, user://playtest-records.jsonl, apart from the save, and its export
public static class PlayRecordFiles
{
    private const string FilePath = "user://playtest-records.jsonl";

    private static PlayRecordStore store;

    private static PlayRecordStore Store => store ??= new PlayRecordStore(ProjectSettings.GlobalizePath(FilePath));

    public static int Count => Store.Count;

    public static void Append(PlayRecord record)
    {
        if (Store.Append(record) is { } problem)
        {
            GD.PushError($"Play data: can't write {FilePath} ({problem})");
            return;
        }
        GD.Print($"Play data: recorded {record.Outcome} ({Store.Count} records)");
    }

    // Through the system's save picker where there is one, otherwise to the clipboard. done gets the sentence the
    // settings sheet shows; a cancelled picker calls nothing
    public static void Export(Action<string> done)
    {
        var load = Store.Load();
        if (load.Problem is { } problem)
        {
            done($"Couldn't export: {problem}");
            return;
        }
        var count = load.Records.Count;
        if (!DisplayServer.HasFeature(DisplayServer.Feature.NativeDialogFile))
        {
            ToClipboard(load.Text, count, done);
            return;
        }
        // The invariant culture keeps a Thai or Hijri phone from naming the file with its own calendar's year
        var name = $"merged-cells-play-data-{DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.jsonl";
        var callback = Callable.From<bool, string[], int>((chosen, paths, _) =>
        {
            if (!chosen || paths.Length == 0)
            {
                GD.Print("Play data: export cancelled");
                return;
            }
            if (Write(paths[0], load.Text) is { } error)
            {
                GD.PushWarning($"Play data: can't write {paths[0]} ({error}); copying to the clipboard instead");
                ToClipboard(load.Text, count, done);
                return;
            }
            GD.Print($"Play data: exported {count} records to {paths[0]}");
            done($"Saved {Maps(count)}.");
        });
        var opened = DisplayServer.FileDialogShow(
            "Export play data", "", name, false, DisplayServer.FileDialogMode.SaveFile, ["*.jsonl"], callback);
        if (opened != Error.Ok)
        {
            GD.PushWarning($"Play data: the save picker didn't open ({opened}); copying to the clipboard instead");
            ToClipboard(load.Text, count, done);
        }
    }

    // On Android the path is a content URI from the Storage Access Framework, which FileAccess opens like a path
    private static string Write(string path, string text)
    {
        using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Write);
        if (file is null)
        {
            return Godot.FileAccess.GetOpenError().ToString();
        }
        file.StoreString(text);
        return file.GetError() is var error and not Error.Ok ? error.ToString() : null;
    }

    private static void ToClipboard(string text, int count, Action<string> done)
    {
        DisplayServer.ClipboardSet(text);
        GD.Print($"Play data: copied {count} records to the clipboard ({text.Length} characters)");
        done($"Copied {Maps(count)} to the clipboard.");
    }

    private static string Maps(int count) => count == 1 ? "1 map" : $"{count} maps";
}
