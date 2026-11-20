using System.Text.Json;
using System.Text.RegularExpressions;
using MergedCells.Core;

namespace MergedCells.Curation;

internal sealed record LevelOnDisk(string Path, Level Level);

// The campaign as promote and retire find it on disk, the check their new texts must pass before anything is written,
// and the gates that move when they change it
internal sealed record CampaignFiles(
    IReadOnlyList<LevelOnDisk> Levels, string ChaptersText, Campaign Campaign, string ConfigText, GameConfig Config, IReadOnlyList<int> Retired)
{
    private static readonly Regex OtherCharacters = new("[^a-z0-9]+", RegexOptions.CultureInvariant);

    // Throws InvalidDataException naming the file that doesn't read
    public static CampaignFiles Load(Workspace workspace)
    {
        var levels = Directory.GetFiles(workspace.LevelsFolder, "*.json")
            .Order(StringComparer.Ordinal)
            .Select(path => new LevelOnDisk(path, Read(workspace, path, LevelFile.Parse)))
            .ToArray();
        var chaptersText = File.ReadAllText(workspace.ChaptersFile);
        var campaign = Read(workspace, workspace.ChaptersFile, text => Campaign.Parse(text, levels.Select(level => level.Level).ToArray()));
        var configText = File.ReadAllText(workspace.ConfigFile);
        var config = Read(workspace, workspace.ConfigFile, GameConfig.Parse);
        var retired = Read(workspace, workspace.RetiredFile, RetiredLevels.Parse);
        return new CampaignFiles(levels, chaptersText, campaign, configText, config, retired);
    }

    // Everything Main's start-up check and the shipped tests would reject; throws what they throw
    public static (Campaign Campaign, GameConfig Config) Check(IReadOnlyList<Level> levels, string chaptersText, string configText)
    {
        var campaign = Campaign.Parse(chaptersText, levels);
        var config = GameConfig.Parse(configText);
        config.CheckRanges(levels);
        _ = CampaignProgress.Of(campaign, new SaveGame(), config);
        return (campaign, config);
    }

    // True for what Load, Check, the line edit and the file system throw for bad data or a file that can't be read or
    // written, which a command reports rather than crashing on
    public static bool IsBadData(Exception exception) =>
        exception is JsonException or InvalidMapException or InvalidDataException or ArgumentOutOfRangeException or InvalidOperationException
            or IOException or UnauthorizedAccessException;

    // Writes every file, or, when one can't be written, puts back the ones already written (a new file is deleted), so a
    // command never leaves a half edit. Throws IOException naming the file
    public static void WriteAll(Workspace workspace, IReadOnlyList<(string Path, string Text)> files)
    {
        var written = new List<(string Path, byte[]? Before)>();
        foreach (var (path, text) in files)
        {
            try
            {
                var before = File.Exists(path) ? File.ReadAllBytes(path) : null;
                // supabase/ isn't in the tests' throwaway repositories
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, text);
                written.Add((path, before));
            }
            catch (Exception problem) when (problem is IOException or UnauthorizedAccessException)
            {
                foreach (var (done, before) in Enumerable.Reverse(written))
                {
                    if (before is null)
                    {
                        File.Delete(done);
                    }
                    else
                    {
                        File.WriteAllBytes(done, before);
                    }
                }
                throw new IOException($"Couldn't write {workspace.Relative(path)}: {problem.Message} Close anything holding it open, or make it writable.", problem);
            }
        }
    }

    // From an empty save, so they're the stars a new player needs
    public static IReadOnlyList<string> GatesMoved(Campaign before, GameConfig beforeConfig, Campaign after, GameConfig afterConfig)
    {
        var old = CampaignProgress.Of(before, new SaveGame(), beforeConfig);
        var now = CampaignProgress.Of(after, new SaveGame(), afterConfig);
        var moved = new List<string>();
        foreach (var chapter in after.Chapters)
        {
            if (chapter.Number > before.Chapters.Count)
            {
                moved.Add($"Chapter {chapter.Number} is new, with a gate of {now.Gate(chapter)} stars.");
            }
            else if (old.Gate(before.Chapters[chapter.Number - 1]) is var was && was != now.Gate(chapter))
            {
                moved.Add($"Chapter {chapter.Number}'s gate: {was} → {now.Gate(chapter)} stars.");
            }
        }
        return moved;
    }

    // "Over the Top" is over-the-top.json: lowercase, with each run of other characters one hyphen (the shipped rule)
    public static string FileNameFor(string name) => OtherCharacters.Replace(name.ToLowerInvariant(), "-").Trim('-') + ".json";

    private static T Read<T>(Workspace workspace, string path, Func<string, T> parse)
    {
        try
        {
            return parse(File.ReadAllText(path));
        }
        catch (Exception problem) when (problem is JsonException or InvalidMapException)
        {
            throw new InvalidDataException($"{workspace.Relative(path)}: {problem.Message}", problem);
        }
    }
}
