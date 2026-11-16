using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using MergedCells.Core;

namespace MergedCells.Curation;

// What generate and concept share: climbing a batch in parallel, and writing its hits, run.json and the page into one
// folder of builds/candidates/
internal static class CandidateBatch
{
    // Candidate i climbs with seed N × 65536 + i, so batches with different seeds never share a climb
    private const ulong SeedStride = 65536;

    private static readonly Regex CandidateFile = new("^c[0-9]+\\.json$", RegexOptions.CultureInvariant);

    // Results are kept by index, so the batch never depends on thread timing
    public static (ClimbedMap?[] Results, TimeSpan Elapsed) Climb(MapClimber climber, int count, ulong seed, Func<int, ClimbRequest> request)
    {
        var results = new ClimbedMap?[count];
        var clock = Stopwatch.StartNew();
        Parallel.For(0, count, i => results[i] = climber.Climb(request(i), unchecked(seed * SeedStride + (ulong)i)));
        clock.Stop();
        return (results, clock.Elapsed);
    }

    // Replaces only the folder's earlier candidates, page and run file; missHint says what to change when nothing hit
    public static CommandResult Write(
        Workspace workspace,
        string folderName,
        int chapter,
        string band,
        BandRecipe recipe,
        RecipeFile recipes,
        GameConfig config,
        ulong seed,
        (ClimbedMap?[] Results, TimeSpan Elapsed) batch,
        PageConcept? concept,
        string missHint)
    {
        var folder = Path.Combine(workspace.CandidatesFolder, folderName);
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

        var climbs = batch.Results.Length;
        var width = climbs.ToString(CultureInfo.InvariantCulture).Length;
        var rater = new DifficultyRater(config.Difficulty);
        var candidates = new List<PageCandidate>();
        for (var i = 0; i < climbs; i++)
        {
            if (batch.Results[i] is not { Distance: 0 } hit)
            {
                continue;
            }
            var number = i + 1;
            var path = Path.Combine(folder, $"c{number.ToString(CultureInfo.InvariantCulture).PadLeft(width, '0')}.json");
            File.WriteAllText(path, LevelFile.Write(new Level(number, $"Candidate {number}", chapter, hit.Map, hit.Solution.Par!.Value)));
            var limits = recipe.OneStarExtraMoves is { } oneStar
                ? StarLimits.Reachable(hit.Map, recipes.StarLimits.MinRoutes, recipes.StarLimits.MaxExtraMoves, oneStar)
                : null;
            var promote = $"dotnet run --project src/MergedCells.Curation -c Release -- promote {workspace.Relative(path)} --name \"NAME\"";
            candidates.Add(new PageCandidate(Path.GetFileName(path), hit, limits, promote, TwistReliance.TrapsThatDontMatter(hit.Map, rater)));
        }
        File.WriteAllText(Path.Combine(folder, "run.json"), new RunFile(chapter, band, seed, recipe.OneStarExtraMoves).Write());
        var page = Path.Combine(folder, "index.html");
        File.WriteAllText(page, CandidatePage.Write(chapter, band, recipe, seed, climbs, Shipped(workspace, config, chapter), candidates, concept));

        var next = candidates.Count == 0
            ? $"None hit the target. Try another --seed, or {missHint}.\n"
            : "Next: open the page in a browser, then run the promote command under each pick, with its name.\n";
        return new CommandResult(
            CommandResult.Done,
            string.Create(
                CultureInfo.InvariantCulture,
                $"{candidates.Count} of {climbs} climbs hit the target, in {batch.Elapsed.TotalSeconds:0.0} s.\nPage: {workspace.Relative(page)}\n{next}"));
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
