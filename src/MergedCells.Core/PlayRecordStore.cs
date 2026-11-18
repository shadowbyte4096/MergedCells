using System.Text;
using System.Text.Json;

namespace MergedCells.Core;

/// <summary>
/// What the records file holds: the records that read, how many lines didn't, the readable lines as an export writes them
/// (each ending in a line break), and a problem when the file couldn't be read at all.
/// </summary>
public sealed record PlayRecordLoad(IReadOnlyList<PlayRecord> Records, int Skipped, string Text, string? Problem);

/// <summary>
/// The play records file: one record per line, only ever added to. A line that doesn't read (half-written when the app was
/// killed, from a newer build, broken) is skipped, never fatal. Plain .NET file code; main thread only.
/// </summary>
public sealed class PlayRecordStore
{
    private int? count;

    public PlayRecordStore(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        Path = path;
    }

    public string Path { get; }

    // The records that read, from the first use (a load), plus each append since; 0 when the file can't be read
    public int Count => count ??= Load().Records.Count;

    public PlayRecordLoad Load()
    {
        string[] lines;
        try
        {
            lines = File.ReadAllLines(Path);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return new PlayRecordLoad([], 0, "", null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new PlayRecordLoad([], 0, "", exception.Message);
        }

        var records = new List<PlayRecord>();
        var text = new StringBuilder();
        var skipped = 0;
        foreach (var line in lines.Where(line => line.Length > 0))
        {
            try
            {
                records.Add(PlayRecordFile.Parse(line));
                text.Append(line).Append('\n');
            }
            catch (Exception exception) when (exception is JsonException or NewerPlayRecordException or InvalidMapException)
            {
                skipped++;
            }
        }
        count = records.Count;
        return new PlayRecordLoad(records, skipped, text.ToString(), null);
    }

    /// <summary>Adds the record as a line of its own. Returns the problem when it can't be written; never throws over I/O.</summary>
    public string? Append(PlayRecord record)
    {
        var line = PlayRecordFile.Write(record) + "\n";
        try
        {
            // A torn last line would otherwise swallow this record into one unreadable line
            File.AppendAllText(Path, EndsInLineBreak() ? line : "\n" + line);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return exception.Message;
        }
        // Only a count already worked out is kept up; otherwise the first use reads the file, this record included
        if (count is { } known)
        {
            count = known + 1;
        }
        return null;
    }

    private bool EndsInLineBreak()
    {
        try
        {
            using var stream = new FileStream(Path, FileMode.Open, FileAccess.Read);
            if (stream.Length == 0)
            {
                return true;
            }
            stream.Seek(-1, SeekOrigin.End);
            return stream.ReadByte() == '\n';
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return true;
        }
    }
}
