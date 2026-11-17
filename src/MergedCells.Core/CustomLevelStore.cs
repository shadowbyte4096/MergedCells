using System.Text.Json;

namespace MergedCells.Core;

/// <summary>
/// The custom levels a folder holds, newest first by import time or else edit time, and one line per file that was
/// skipped and why. <paramref name="Unreadable"/> holds the ID of every skipped file named for an ID (in any case, as
/// Windows names ignore it), which nothing may write over. <paramref name="Listed"/> is false when the folder exists but
/// couldn't be listed, so any ID's file may be there.
/// </summary>
public sealed record CustomLevelLoad(
    IReadOnlyList<CustomLevel> Levels, IReadOnlyList<string> Skipped, IReadOnlySet<Guid>? Unreadable = null, bool Listed = true);

/// <summary>
/// One file per custom level, <c>&lt;id&gt;.json</c>, in one folder. Plain .NET file code, main thread only. Nothing here
/// throws for a file problem: a file that can't be read is skipped and left exactly as it is, and a write that fails
/// returns the problem.
/// </summary>
public sealed class CustomLevelStore(string folder)
{
    public string Folder { get; } = folder ?? throw new ArgumentNullException(nameof(folder));

    public CustomLevelLoad LoadAll()
    {
        if (!Directory.Exists(Folder))
        {
            return new CustomLevelLoad([], [], new HashSet<Guid>());
        }
        string[] paths;
        try
        {
            paths = Directory.GetFiles(Folder, "*.json");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new CustomLevelLoad([], [$"the folder can't be listed ({exception.Message})"], new HashSet<Guid>(), Listed: false);
        }
        var levels = new List<CustomLevel>();
        var skipped = new List<string>();
        var unreadable = new HashSet<Guid>();
        foreach (var path in paths)
        {
            var name = Path.GetFileName(path);
            try
            {
                var level = CustomLevelFile.Parse(File.ReadAllText(path));
                // So two files can never claim one level
                if (name != FileName(level.Id))
                {
                    skipped.Add($"{name}: holds level {level.Id:D}, so it should be named {FileName(level.Id)}");
                    AddUnreadable(unreadable, name);
                    continue;
                }
                levels.Add(level);
            }
            catch (NewerCustomLevelException exception)
            {
                skipped.Add($"{name}: {exception.Message}");
                AddUnreadable(unreadable, name);
            }
            catch (Exception exception) when (exception is JsonException or InvalidMapException)
            {
                skipped.Add($"{name}: broken ({exception.Message})");
                AddUnreadable(unreadable, name);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                skipped.Add($"{name}: can't be read ({exception.Message})");
                AddUnreadable(unreadable, name);
            }
        }
        return new CustomLevelLoad(
            levels
                .OrderByDescending(level => level.Imported ?? level.Edited)
                .ThenBy(level => level.Name, StringComparer.Ordinal)
                .ThenBy(level => level.Id)
                .ToArray(),
            skipped,
            unreadable);
    }

    // A skipped file named for an ID, in any case (Windows names ignore it), is what writing that ID would replace
    private static void AddUnreadable(HashSet<Guid> unreadable, string name)
    {
        if (name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && Guid.TryParseExact(name[..^5], "D", out var id))
        {
            unreadable.Add(id);
        }
    }

    /// <summary>
    /// Writes the level to its file through a temporary file and one rename, so a crash mid-write leaves the old file whole.
    /// Returns the problem when it can't, or null.
    /// </summary>
    public string? Write(CustomLevel level)
    {
        ArgumentNullException.ThrowIfNull(level);
        var path = Path.Combine(Folder, FileName(level.Id));
        var temp = path + ".tmp";
        try
        {
            Directory.CreateDirectory(Folder);
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(CustomLevelFile.Write(level));
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }
            File.Move(temp, path, overwrite: true);
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return exception.Message;
        }
    }

    /// <summary>Deletes the level's file; a file that isn't there counts as deleted. Returns the problem when it can't, or null.</summary>
    public string? Delete(Guid id)
    {
        try
        {
            File.Delete(Path.Combine(Folder, FileName(id)));
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return exception.Message;
        }
    }

    private static string FileName(Guid id) => $"{id:D}.json";
}
