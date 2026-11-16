using MergedCells.Core;

namespace MergedCells.Curation;

// generate <chapter> <band> [--seed N] [--count N]: climbs a band's candidates in parallel and writes the hits, run.json
// and the page into builds/candidates/chapter-<n>-<band>-seed-<N>/
internal static class GenerateCommand
{
    public const string Usage = "Usage: generate <chapter> <band> [--seed N] [--count N]\n";

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

        var climber = new MapClimber(config.Generator, config.Difficulty, config.Climb);
        var batch = CandidateBatch.Climb(climber, count ?? band.Candidates, seed, i =>
        {
            var size = band.MinSize + i % (band.MaxSize - band.MinSize + 1);
            return new ClimbRequest(new MapRequest(size, band.MinPar, band.MaxPar, chapter.Bends, band.Twists), band.Target);
        });
        return CandidateBatch.Write(
            workspace,
            $"chapter-{chapterNumber}-{bandName}-seed-{seed}",
            chapterNumber,
            bandName,
            band,
            recipes,
            config,
            seed,
            batch,
            null,
            "loosen the band's target in src/MergedCells.Curation/recipes.json");
    }
}
