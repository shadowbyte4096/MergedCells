using System.Collections.Generic;
using Godot;
using MergedCells.Core;

namespace MergedCells;

// The play records file, user://play-records.jsonl, apart from the save: records wait here until the server has them all,
// and then ServerQueue empties it
public static class PlayRecordFiles
{
    private const string FilePath = "user://play-records.jsonl";

    private static PlayRecordStore store;

    private static PlayRecordStore Store => store ??= new PlayRecordStore(ProjectSettings.GlobalizePath(FilePath));

    public static int Count => Store.Count;

    public static IReadOnlyList<RecordLine> ReadFrom(long offset, int maxLines) => Store.ReadFrom(offset, maxLines);

    public static bool EndsAt(long offset) => Store.EndsAt(offset);

    public static void Append(PlayRecord record)
    {
        if (Store.Append(record) is { } problem)
        {
            GD.PushError($"Play data: can't write {FilePath} ({problem})");
            return;
        }
        GD.Print($"Play data: recorded {record.Outcome} ({Store.Count} records)");
    }

    // The problem when the file can't be deleted, or null
    public static string Delete()
    {
        var problem = Store.Delete();
        if (problem is null)
        {
            GD.Print($"Play data: deleted {FilePath}");
        }
        else
        {
            GD.PushError($"Play data: can't delete {FilePath} ({problem})");
        }
        return problem;
    }
}
