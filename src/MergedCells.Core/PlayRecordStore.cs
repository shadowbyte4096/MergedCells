using System.Text;
using System.Text.Json;

namespace MergedCells.Core;

/// <summary>
/// What the records file holds: the records that read, how many lines didn't, and a problem when the file couldn't be
/// read at all.
/// </summary>
public sealed record PlayRecordLoad(IReadOnlyList<PlayRecord> Records, int Skipped, string? Problem);

/// <summary>
/// One complete line of the records file: the byte offset just past its line break, its text, and its record, or null
/// when the line doesn't read.
/// </summary>
public sealed record RecordLine(long End, string Text, PlayRecord? Record);

/// <summary>
/// The play records file: one record per line, added to as maps end and emptied once the server holds every record in it
/// (see <see cref="ServerQueue"/>), so the device keeps only records waiting to be sent. A line that doesn't read
/// (half-written when the app was killed, from a newer build, broken) is skipped, never fatal. Plain .NET file code; main
/// thread only.
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
            return new PlayRecordLoad([], 0, null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new PlayRecordLoad([], 0, exception.Message);
        }

        var records = new List<PlayRecord>();
        var skipped = 0;
        foreach (var line in lines.Where(line => line.Length > 0))
        {
            if (Read(line) is { } record)
            {
                records.Add(record);
            }
            else
            {
                skipped++;
            }
        }
        count = records.Count;
        return new PlayRecordLoad(records, skipped, null);
    }

    /// <summary>
    /// Whether the file is exactly <paramref name="offset"/> bytes long, so everything up to that point is everything in it.
    /// False when the file is missing or can't be read; never throws over I/O.
    /// </summary>
    public bool EndsAt(long offset)
    {
        try
        {
            var file = new FileInfo(Path);
            return file.Exists && file.Length == offset;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Up to <paramref name="maxLines"/> complete lines from the byte <paramref name="offset"/> on. A torn last line isn't
    /// returned until a later append finishes it off. An offset past the end reads from the start, since the file was
    /// replaced. A missing or unreadable file gives no lines; never throws over I/O.
    /// </summary>
    public IReadOnlyList<RecordLine> ReadFrom(long offset, int maxLines)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxLines, 1);
        var lines = new List<RecordLine>();
        try
        {
            using var stream = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var position = offset > stream.Length ? 0 : offset;
            stream.Seek(position, SeekOrigin.Begin);
            var line = new MemoryStream();
            var buffer = new byte[64 * 1024];
            int read;
            while (lines.Count < maxLines && (read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (var index = 0; index < read && lines.Count < maxLines; index++)
                {
                    position++;
                    if (buffer[index] != (byte)'\n')
                    {
                        line.WriteByte(buffer[index]);
                        continue;
                    }
                    var text = Encoding.UTF8.GetString(line.GetBuffer(), 0, (int)line.Length).TrimEnd('\r');
                    line.SetLength(0);
                    lines.Add(new RecordLine(position, text, text.Length > 0 ? Read(text) : null));
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
        return lines;
    }

    // A torn, newer or broken line is skipped, never fatal
    private static PlayRecord? Read(string line)
    {
        try
        {
            return PlayRecordFile.Parse(line);
        }
        catch (Exception exception) when (exception is JsonException or NewerPlayRecordException or InvalidMapException)
        {
            return null;
        }
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

    /// <summary>Deletes the file and every record in it. Returns the problem when it can't; never throws over I/O.</summary>
    public string? Delete()
    {
        try
        {
            File.Delete(Path);
        }
        catch (Exception exception) when (exception is DirectoryNotFoundException)
        {
            // Nothing there to delete
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return exception.Message;
        }
        count = 0;
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
