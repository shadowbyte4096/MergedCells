using System.Globalization;
using System.Text;
using System.Text.Json;

namespace MergedCells.Core;

public enum SaveOutcome
{
    Missing,
    Loaded,
    Broken,
    Newer,
    Unreadable,
}

// Problem is null for Missing and Loaded, and the file's version for Newer
public sealed record SaveLoad(SaveGame Save, SaveOutcome Outcome, string? Problem);

/// <summary>
/// The save file on disk. Loading never throws over the file's content: a broken file is set aside and a fresh save
/// starts, and a file from a newer build, or one that can't be read, is left alone and nothing is written.
/// </summary>
public sealed class SaveStore
{
    public SaveStore(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        Path = path;
    }

    public string Path { get; }

    public bool CanWrite { get; private set; }

    private string BrokenPath =>
        System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(Path) ?? "",
            System.IO.Path.GetFileNameWithoutExtension(Path) + ".broken" + System.IO.Path.GetExtension(Path));

    private string TempPath => Path + ".tmp";

    public SaveLoad Load()
    {
        string text;
        try
        {
            text = File.ReadAllText(Path);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return Loaded(new SaveGame(), SaveOutcome.Missing, null, canWrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Loaded(new SaveGame(), SaveOutcome.Unreadable, exception.Message, canWrite: false);
        }

        try
        {
            return Loaded(SaveGame.Parse(text), SaveOutcome.Loaded, null, canWrite: true);
        }
        catch (NewerSaveException exception)
        {
            return Loaded(new SaveGame(), SaveOutcome.Newer, exception.Version.ToString(CultureInfo.InvariantCulture), canWrite: false);
        }
        catch (JsonException exception)
        {
            try
            {
                File.Move(Path, BrokenPath, overwrite: true);
            }
            catch (Exception moveException) when (moveException is IOException or UnauthorizedAccessException)
            {
                // Writing a fresh save over a broken file that couldn't be kept would lose it for good
                return Loaded(new SaveGame(), SaveOutcome.Unreadable, $"{exception.Message} It couldn't be set aside: {moveException.Message}", canWrite: false);
            }
            return Loaded(new SaveGame(), SaveOutcome.Broken, exception.Message, canWrite: true);
        }
    }

    /// <summary>Writes the save through a temporary file and one rename, so a write cut short leaves the old file whole.
    /// Returns null once written, or the problem. Throws <see cref="InvalidOperationException"/> when writing is off:
    /// before <see cref="Load"/>, and after a newer or unreadable file.</summary>
    public string? Write(SaveGame save)
    {
        ArgumentNullException.ThrowIfNull(save);
        if (!CanWrite)
        {
            throw new InvalidOperationException($"{Path} can't be written: it hasn't been loaded, or it's from a newer build or unreadable.");
        }

        try
        {
            if (System.IO.Path.GetDirectoryName(Path) is { Length: > 0 } directory)
            {
                Directory.CreateDirectory(directory);
            }
            var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(save.ToJson());
            using (var stream = new FileStream(TempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(TempPath, Path, overwrite: true);
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return exception.Message;
        }
    }

    private SaveLoad Loaded(SaveGame save, SaveOutcome outcome, string? problem, bool canWrite)
    {
        CanWrite = canWrite;
        return new SaveLoad(save, outcome, problem);
    }
}
