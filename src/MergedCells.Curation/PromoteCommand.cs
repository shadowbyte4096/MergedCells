using System.Globalization;
using System.Text;
using MergedCells.Core;

namespace MergedCells.Curation;

// promote <file> --name "<name>" [--after <id>] [--band <band>]: makes a candidate a level under the next unused ID,
// places it in its chapter and sets its star limits. Every new text is checked together before any file is written
internal static class PromoteCommand
{
    public const string Usage = "Usage: promote <candidate file> --name \"<name>\" [--after <id>] [--band <band>]\n";

    // Chunk 12 found "6 · Round the Back" cut off at phone width
    private const int LongestComfortableName = 12;

    public static CommandResult Run(Workspace workspace, IReadOnlyList<string> args)
    {
        string file;
        string name;
        int? after;
        string? bandName;
        try
        {
            var arguments = Arguments.Parse(args, "--name", "--after", "--band");
            if (arguments.Positional.Count != 1)
            {
                throw new UsageException("promote needs one candidate file.");
            }
            file = Path.GetFullPath(arguments.Positional[0]);
            name = (arguments.Option("--name") ?? throw new UsageException("promote needs a --name for the level.")).Trim();
            after = arguments.Option("--after") is { } given ? Arguments.Whole(given, "--after", 1) : null;
            bandName = arguments.Option("--band");
        }
        catch (UsageException mistake)
        {
            return new CommandResult(CommandResult.UsageMistake, $"{mistake.Message}\n{Usage}");
        }

        try
        {
            return Promote(workspace, file, name, after, bandName);
        }
        catch (Exception problem) when (CampaignFiles.IsBadData(problem))
        {
            return Refuse($"{problem.Message}\nNothing was written.");
        }
    }

    private static CommandResult Promote(Workspace workspace, string file, string name, int? after, string? bandName)
    {
        if (!File.Exists(file))
        {
            return Refuse($"There's no file at {file}.");
        }
        var files = CampaignFiles.Load(workspace);
        var recipes = Recipes.Parse(File.ReadAllText(workspace.RecipesFile));
        var candidate = LevelFile.Parse(File.ReadAllText(file));
        var par = Solver.Solve(candidate.Map).Par;
        if (par != candidate.Par)
        {
            return Refuse($"The file says par {candidate.Par}, but the solver gives {par?.ToString(CultureInfo.InvariantCulture) ?? "no par"}. Fix the file, or promote a fresh candidate.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Refuse("The level's name is blank.");
        }
        var fileName = CampaignFiles.FileNameFor(name);
        if (fileName == ".json")
        {
            return Refuse($"\"{name}\" has no letters or digits to name its file. Pick another name.");
        }
        var levelPath = Path.Combine(workspace.LevelsFolder, fileName);
        if (File.Exists(levelPath))
        {
            return Refuse($"{workspace.Relative(levelPath)} is already taken. Pick another name.");
        }

        var id = RetiredLevels.NextId(files.Levels.Select(level => level.Level.Id), files.Retired);
        var chapterNumber = candidate.Chapter;
        var chapters = files.Campaign.Chapters.Select(ChapterEntry.From).ToList();
        int place;
        if (chapterNumber <= chapters.Count)
        {
            var ids = chapters[chapterNumber - 1].LevelIds.ToList();
            place = ids.Count;
            if (after is { } afterId)
            {
                place = ids.IndexOf(afterId) + 1;
                if (place == 0)
                {
                    return Refuse($"Level {afterId} isn't in chapter {chapterNumber}, which holds {string.Join(", ", ids)}.");
                }
            }
            ids.Insert(place, id);
            chapters[chapterNumber - 1] = chapters[chapterNumber - 1] with { LevelIds = ids };
        }
        else if (chapterNumber == chapters.Count + 1)
        {
            if (after is { } afterId)
            {
                return Refuse($"Level {afterId} isn't in chapter {chapterNumber}, which is new.");
            }
            if (!recipes.Chapters.TryGetValue(chapterNumber, out var recipe))
            {
                return Refuse($"Chapter {chapterNumber} is new, and recipes.json has no chapter {chapterNumber} to name it.");
            }
            chapters.Add(new ChapterEntry(recipe.Name, [id], []));
            place = 0;
        }
        else
        {
            return Refuse(
                $"The candidate is in chapter {chapterNumber}, but chapters.json has {chapters.Count}, so the next new chapter is {chapters.Count + 1}. Fill the chapters in order.");
        }

        int? oneStarExtraMoves;
        var runPath = Path.Combine(Path.GetDirectoryName(file)!, "run.json");
        if (bandName is not null)
        {
            if (!recipes.Chapters.TryGetValue(chapterNumber, out var recipe) || !recipe.Bands.TryGetValue(bandName, out var band))
            {
                return Refuse($"recipes.json has no band \"{bandName}\" in chapter {chapterNumber}.");
            }
            oneStarExtraMoves = band.OneStarExtraMoves;
        }
        else
        {
            oneStarExtraMoves = File.Exists(runPath) ? RunFile.Parse(File.ReadAllText(runPath)).OneStarExtraMoves : null;
        }
        StarsConfig? limits = null;
        if (oneStarExtraMoves is { } oneStar)
        {
            limits = StarLimits.Reachable(candidate.Map, recipes.StarLimits.MinRoutes, recipes.StarLimits.MaxExtraMoves, oneStar);
            if (limits is null)
            {
                return Refuse(
                    $"Fewer than {recipes.StarLimits.MinRoutes} winning routes finish within par + {recipes.StarLimits.MaxExtraMoves}, so no 2-star limit is reachable. Pick another candidate.");
            }
        }

        var level = new Level(id, name, chapterNumber, candidate.Map, candidate.Par, candidate.Tutorial);
        var levelText = LevelFile.Write(level);
        var chaptersText = ChapterFile.Write(chapters);
        var configText = limits is null ? files.ConfigText : LevelStarsEdit.Add(files.ConfigText, id, limits);
        var levels = files.Levels.Select(onDisk => onDisk.Level).Append(LevelFile.Parse(levelText)).ToArray();
        var (campaign, config) = CampaignFiles.Check(levels, chaptersText, configText);

        CampaignFiles.WriteAll(
            workspace,
            [
                (levelPath, levelText),
                (workspace.ChaptersFile, chaptersText),
                (workspace.ConfigFile, configText),
                (workspace.ServerLevelsFile, ServerLevels.Write(levels)),
            ]);

        var output = new StringBuilder()
            .Append($"Promoted as level {id}: {workspace.Relative(levelPath)}, place {place + 1} of {chapters[chapterNumber - 1].LevelIds.Count} in chapter {chapterNumber}.\n")
            .Append(limits is null
                ? "Star limits: the chapter's thresholds.\n"
                : $"Star limits: 2 stars within par + {limits.TwoStarMinExtraMoves}, 1 star within par + {limits.OneStarMinExtraMoves}.\n");
        if (name.Length > LongestComfortableName)
        {
            output.Append($"Warning: \"{name}\" is over 12 characters, and may be cut off at phone width.\n");
        }
        output.Append(Gates(CampaignFiles.GatesMoved(files.Campaign, files.Config, campaign, config)))
            .Append(ServerLevelsCommand.RunOnServer)
            .Append("Next: run dotnet test, then play the chapter.\n");
        return new CommandResult(CommandResult.Done, output.ToString());
    }

    public static string Gates(IReadOnlyList<string> moved) =>
        moved.Count == 0 ? "No gates moved.\n" : "Gates that moved:\n" + string.Concat(moved.Select(line => $"  {line}\n"));

    private static CommandResult Refuse(string message) => new(CommandResult.Refused, message + "\n");
}
