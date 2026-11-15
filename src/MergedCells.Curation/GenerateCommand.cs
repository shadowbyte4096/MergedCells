using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using MergedCells.Core;

namespace MergedCells.Curation;

// generate <chapter> <band> [--seed N] [--count N]: climbs a band's candidates in parallel and writes the hits, run.json
// and the page into builds/candidates/chapter-<n>-<band>-seed-<N>/
internal static class GenerateCommand
{
    public const string Usage = "Usage: generate <chapter> <band> [--seed N] [--count N]\n";

    // Candidate i climbs with seed N × 65536 + i, so batches with different seeds never share a climb
    private const ulong SeedStride = 65536;

    private static readonly Regex CandidateFile = new("^c[0-9]+\\.json$", RegexOptions.CultureInvariant);

    public static CommandResult Run(Workspace workspace, IReadOnlyList<string> args)
    {
        int chapterNumber;
        string bandName;
        ulong seed;
        int? count;
        try
        {
            var arguments = Arguments.Parse(args, "--seed", "--count");
            if (arguments.Positional.Count != 2)
            {
                throw new UsageException("generate needs a chapter number and a band name.");
            }
            chapterNumber = Arguments.Whole(arguments.Positional[0], "The chapter", 1);
            bandName = arguments.Positional[1];
            seed = arguments.Option("--seed") is { } given ? Arguments.Seed(given) : 1;
            count = arguments.Option("--count") is { } counted ? Arguments.Whole(counted, "--count", 1) : null;
        }
        catch (UsageException mistake)
        {
            return new CommandResult(CommandResult.UsageMistake, $"{mistake.Message}\n{Usage}");
        }

        try
        {
            return Generate(workspace, chapterNumber, bandName, seed, count);
        }
        catch (Exception problem) when (CampaignFiles.IsBadData(problem))
        {
            return new CommandResult(CommandResult.Refused, $"{problem.Message}\nFix it and run generate again.\n");
        }
    }

    private static CommandResult Generate(Workspace workspace, int chapterNumber, string bandName, ulong seed, int? count)
    {
        var recipes = Recipes.Parse(File.ReadAllText(workspace.RecipesFile));
        var config = GameConfig.Parse(File.ReadAllText(workspace.ConfigFile));
        if (!recipes.Chapters.TryGetValue(chapterNumber, out var chapter))
        {
            return new CommandResult(
                CommandResult.Refused,
                $"recipes.json has no chapter {chapterNumber}. It has chapters {string.Join(", ", recipes.Chapters.Keys.Order())}.\n");
        }
        if (!chapter.Bands.TryGetValue(bandName, out var band))
        {
            return new CommandResult(
                CommandResult.Refused,
                $"Chapter {chapterNumber}'s recipe has no band \"{bandName}\". It has {string.Join(", ", chapter.Bands.Keys.Order(StringComparer.Ordinal))}.\n");
        }

        var climbs = count ?? band.Candidates;
        var climber = new MapClimber(config.Generator, config.Difficulty, config.Climb);
        var results = new ClimbedMap?[climbs];
        var clock = Stopwatch.StartNew();
        Parallel.For(0, climbs, i =>
        {
            var size = band.MinSize + i % (band.MaxSize - band.MinSize + 1);
            var request = new ClimbRequest(new MapRequest(size, band.MinPar, band.MaxPar, chapter.Bends, band.Twists), band.Target);
            results[i] = climber.Climb(request, unchecked(seed * SeedStride + (ulong)i));
        });
        clock.Stop();

        var folder = Path.Combine(workspace.CandidatesFolder, $"chapter-{chapterNumber}-{bandName}-seed-{seed}");
        Directory.CreateDirectory(folder);
        var gdignore = Path.Combine(workspace.CandidatesFolder, ".gdignore");
        if (!File.Exists(gdignore))
        {
            File.WriteAllText(gdignore, "");
        }
        foreach (var file in Directory.GetFiles(folder))
        {
            var name = Path.GetFileName(file);
            if (CandidateFile.IsMatch(name) || name is "index.html" or "run.json")
            {
                File.Delete(file);
            }
        }

        var width = climbs.ToString(CultureInfo.InvariantCulture).Length;
        var candidates = new List<PageCandidate>();
        for (var i = 0; i < climbs; i++)
        {
            if (results[i] is not { Distance: 0 } hit)
            {
                continue;
            }
            var number = i + 1;
            var path = Path.Combine(folder, $"c{number.ToString(CultureInfo.InvariantCulture).PadLeft(width, '0')}.json");
            File.WriteAllText(path, LevelFile.Write(new Level(number, $"Candidate {number}", chapterNumber, hit.Map, hit.Solution.Par!.Value)));
            var limits = band.OneStarExtraMoves is { } oneStar
                ? StarLimits.Reachable(hit.Map, recipes.StarLimits.MinRoutes, recipes.StarLimits.MaxExtraMoves, oneStar)
                : null;
            var promote = $"dotnet run --project src/MergedCells.Curation -c Release -- promote {workspace.Relative(path)} --name \"NAME\"";
            candidates.Add(new PageCandidate(Path.GetFileName(path), hit, limits, promote));
        }
        File.WriteAllText(Path.Combine(folder, "run.json"), new RunFile(chapterNumber, bandName, seed, band.OneStarExtraMoves).Write());
        var page = Path.Combine(folder, "index.html");
        File.WriteAllText(page, CandidatePage.Write(chapterNumber, bandName, band, seed, climbs, Shipped(workspace, config, chapterNumber), candidates));

        var next = candidates.Count == 0
            ? "None hit the target. Try another --seed, or loosen the band's target in src/MergedCells.Curation/recipes.json.\n"
            : "Next: open the page in a browser, then run the promote command under each pick, with its name.\n";
        return new CommandResult(
            CommandResult.Done,
            string.Create(
                CultureInfo.InvariantCulture,
                $"{candidates.Count} of {climbs} climbs hit the target, in {clock.Elapsed.TotalSeconds:0.0} s.\nPage: {workspace.Relative(page)}\n{next}"));
    }

    // The chapter's levels in play order, scored, for the page; none for a chapter chapters.json doesn't have yet
    private static IReadOnlyList<PageLevel> Shipped(Workspace workspace, GameConfig config, int chapterNumber)
    {
        var levels = Directory.GetFiles(workspace.LevelsFolder, "*.json").Select(path => LevelFile.Parse(File.ReadAllText(path))).ToArray();
        var campaign = Campaign.Parse(File.ReadAllText(workspace.ChaptersFile), levels);
        if (chapterNumber > campaign.Chapters.Count)
        {
            return [];
        }
        var rater = new DifficultyRater(config.Difficulty);
        return campaign.Chapters[chapterNumber - 1].Levels
            .Select(level => new PageLevel(level, rater.Score(rater.Measure(level.Map)!, DifficultyMode.Campaign)))
            .ToArray();
    }
}
