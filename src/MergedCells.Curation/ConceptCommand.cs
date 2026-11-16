using MergedCells.Core;

namespace MergedCells.Curation;

// concept <name> [--seed N] [--count N]: climbs candidates from concepts/<name>.json's skeleton toward the band it names,
// and writes them as generate does, into builds/candidates/concept-<name>-seed-<N>/
internal static class ConceptCommand
{
    public const string Usage = "Usage: concept <name> [--seed N] [--count N]\n";

    public static CommandResult Run(Workspace workspace, IReadOnlyList<string> args)
    {
        string name;
        ulong seed;
        int? count;
        try
        {
            var arguments = Arguments.Parse(args, "--seed", "--count");
            if (arguments.Positional.Count != 1)
            {
                throw new UsageException("concept needs the concept's name.");
            }
            name = arguments.Positional[0];
            seed = arguments.Option("--seed") is { } given ? Arguments.Seed(given) : 1;
            count = arguments.Option("--count") is { } counted ? Arguments.Whole(counted, "--count", 1) : null;
        }
        catch (UsageException mistake)
        {
            return new CommandResult(CommandResult.UsageMistake, $"{mistake.Message}\n{Usage}");
        }

        if (!ConceptFile.Name.IsMatch(name))
        {
            return new CommandResult(
                CommandResult.Refused, $"\"{name}\" isn't a concept name: use lowercase words joined by hyphens, such as \"pillar-field\".\n");
        }
        var file = Path.Combine(workspace.ConceptsFolder, $"{name}.json");
        if (!File.Exists(file))
        {
            return new CommandResult(CommandResult.Refused, $"There's no concept file {workspace.Relative(file)}.\n");
        }
        try
        {
            return Climb(workspace, name, ConceptFile.Parse(File.ReadAllText(file)), seed, count);
        }
        catch (Exception problem) when (CampaignFiles.IsBadData(problem) || problem is ArgumentException)
        {
            return new CommandResult(CommandResult.Refused, $"{problem.Message}\nFix it and run concept again.\n");
        }
    }

    private static CommandResult Climb(Workspace workspace, string name, Concept concept, ulong seed, int? count)
    {
        var recipes = Recipes.Parse(File.ReadAllText(workspace.RecipesFile));
        var config = GameConfig.Parse(File.ReadAllText(workspace.ConfigFile));
        if (!recipes.Chapters.TryGetValue(concept.Chapter, out var chapter))
        {
            return new CommandResult(
                CommandResult.Refused,
                $"The concept names chapter {concept.Chapter}, which recipes.json doesn't have. It has chapters {string.Join(", ", recipes.Chapters.Keys.Order())}.\n");
        }
        if (!chapter.Bands.TryGetValue(concept.Band, out var band))
        {
            return new CommandResult(
                CommandResult.Refused,
                $"The concept names band \"{concept.Band}\", which chapter {concept.Chapter}'s recipe doesn't have. It has {string.Join(", ", chapter.Bands.Keys.Order(StringComparer.Ordinal))}.\n");
        }

        var size = concept.Skeleton.Size;
        var recipe = new BandRecipe(size, size, concept.MinPar, concept.MaxPar, concept.Twists, concept.Candidates, band.OneStarExtraMoves, band.Target);
        var generator = config.Generator with { MaxWallRun = concept.MaxWallRun ?? config.Generator.MaxWallRun };
        var climber = new MapClimber(generator, config.Difficulty, config.Climb);
        var request = new ClimbRequest(
            new MapRequest(size, concept.MinPar, concept.MaxPar, concept.Bends, concept.Twists), band.Target, DifficultyMode.Campaign, concept.Skeleton);
        // Checked once here, so a skeleton no start fits is refused rather than failing inside every parallel climb
        StartAndEnd.Starts(concept.Skeleton, generator);
        var batch = CandidateBatch.Climb(climber, count ?? concept.Candidates, seed, _ => request);
        return CandidateBatch.Write(
            workspace,
            $"concept-{name}-seed-{seed}",
            concept.Chapter,
            concept.Band,
            recipe,
            recipes,
            config,
            seed,
            batch,
            new PageConcept(name, concept.MaxWallRun, concept.Blocks, concept.Tiles),
            $"change the skeleton or the par range in src/MergedCells.Curation/concepts/{name}.json");
    }
}
