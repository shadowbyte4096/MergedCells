using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using MergedCells.Core;

namespace MergedCells.Curation;

// The recipe file (recipes.json beside the tool), read as strictly as the game's own files. Only the tool reads it, so a
// mistake in it stops the tool and never the game
internal sealed record RecipeFile(StarLimitRecipe StarLimits, IReadOnlyDictionary<int, ChapterRecipe> Chapters);

// Chunk 12's star-limit rule: the 2-star limit is the first length by which MinRoutes winning routes past par finish,
// looking up to MaxExtraMoves past par
internal sealed record StarLimitRecipe(int MinRoutes, int MaxExtraMoves);

// Name is used only when promote starts the chapter in chapters.json
internal sealed record ChapterRecipe(string Name, bool Bends, IReadOnlyDictionary<string, BandRecipe> Bands);

// OneStarExtraMoves null leaves a promoted level on its chapter's star thresholds
internal sealed record BandRecipe(
    int MinSize,
    int MaxSize,
    int MinPar,
    int MaxPar,
    TwistDensities? Twists,
    int Candidates,
    int? OneStarExtraMoves,
    DifficultyTarget Target);

internal static class Recipes
{
    private const char ByteOrderMark = (char)0xFEFF;

    private static readonly Regex BandName = new("^[a-z]+(-[a-z]+)*$", RegexOptions.CultureInvariant);

    /// <summary>
    /// Throws <see cref="JsonException"/>, naming the problem, when a value is missing, null or out of range, when a key
    /// is unknown or repeated, when a band's name isn't lowercase words joined by hyphens, or when the text isn't JSON.
    /// </summary>
    public static RecipeFile Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        var text = json.TrimStart(ByteOrderMark);
        var file = JsonSerializer.Deserialize(text, RecipeJsonContext.Default.RecipeFileJson)
            ?? throw new JsonException("The recipe file is null.");
        JsonKeys.RejectDuplicates(text, "recipe file");

        var starLimits = file.StarLimits ?? throw Problem("starLimits", "is null");
        Require(starLimits.MinRoutes >= 1, "starLimits.minRoutes", "must be at least 1");
        Require(starLimits.MaxExtraMoves is >= 1 and <= 20, "starLimits.maxExtraMoves", "must be from 1 to 20");
        var chapters = new Dictionary<int, ChapterRecipe>();
        foreach (var (number, chapter) in file.Chapters ?? throw Problem("chapters", "is null"))
        {
            var path = $"chapters.{number}";
            Require(number >= 1, path, "is a chapter number, so it must be at least 1");
            chapters[number] = Chapter(chapter ?? throw Problem(path, "is null"), path);
        }
        return new RecipeFile(new StarLimitRecipe(starLimits.MinRoutes, starLimits.MaxExtraMoves), chapters);
    }

    private static ChapterRecipe Chapter(RecipeChapterJson chapter, string path)
    {
        var name = chapter.Name ?? throw Problem($"{path}.name", "is null");
        Require(!string.IsNullOrWhiteSpace(name), $"{path}.name", "is blank");
        var bands = new Dictionary<string, BandRecipe>(StringComparer.Ordinal);
        foreach (var (bandName, band) in chapter.Bands ?? throw Problem($"{path}.bands", "is null"))
        {
            var bandPath = $"{path}.bands.{bandName}";
            Require(BandName.IsMatch(bandName), bandPath, "needs a name of lowercase words joined by hyphens, such as \"hard\" or \"last-one\"");
            bands[bandName] = Band(band ?? throw Problem(bandPath, "is null"), bandPath);
        }
        return new ChapterRecipe(name, chapter.Bends, bands);
    }

    private static BandRecipe Band(RecipeBandJson band, string path)
    {
        var sizes = band.Sizes ?? throw Problem($"{path}.sizes", "is null");
        Require(sizes is [>= 2 and <= 100, >= 2 and <= 100] && sizes[0] <= sizes[1], $"{path}.sizes", "must be [min, max], from 2 to 100, with min no more than max");
        var par = band.Par ?? throw Problem($"{path}.par", "is null");
        Require(par is [>= 1, _] && par[0] <= par[1], $"{path}.par", "must be [min, max], with min at least 1 and no more than max");
        TwistDensities? twists = null;
        if (band.Twists is { } given)
        {
            foreach (var (percent, key) in new[]
            {
                (given.WallPercent, "wallPercent"), (given.StickyPercent, "stickyPercent"), (given.TrapPercent, "trapPercent"), (given.TeleporterPercent, "teleporterPercent"),
            })
            {
                Require(percent is >= 0 and <= 100, $"{path}.twists.{key}", "must be from 0 to 100");
            }
            twists = new TwistDensities(given.WallPercent, given.StickyPercent, given.TrapPercent, given.TeleporterPercent);
        }
        Require(band.Candidates >= 1, $"{path}.candidates", "must be at least 1");
        Require(band.OneStarExtraMoves is null or >= 0, $"{path}.oneStarExtraMoves", "must be null or at least 0");
        return new BandRecipe(sizes[0], sizes[1], par[0], par[1], twists, band.Candidates, band.OneStarExtraMoves, Target(band.Target, $"{path}.target"));
    }

    private static DifficultyTarget Target(RecipeTargetJson? target, string path)
    {
        if (target is null)
        {
            throw Problem(path, "is null");
        }
        Require(target.MinScore is >= 0 and <= 100, $"{path}.minScore", "must be from 0 to 100");
        Require(target.MaxScore >= target.MinScore && target.MaxScore <= 100, $"{path}.maxScore", "must be from minScore to 100");
        Require(target.MinTwistReliance >= 0, $"{path}.minTwistReliance", "must be at least 0");
        MetricMinimums? minimums = null;
        if (target.Minimums is { } given)
        {
            foreach (var (least, key) in new[]
            {
                ((long?)given.AwayMoves, "awayMoves"), (given.GreedyGap, "greedyGap"), (given.Decoys, "decoys"), (given.NearOptimalRoutes, "nearOptimalRoutes"), (given.BruteForceExtraMoves, "bruteForceExtraMoves"),
            })
            {
                Require(least is null or >= 0, $"{path}.minimums.{key}", "must be at least 0");
            }
            minimums = new MetricMinimums(given.AwayMoves, given.GreedyGap, given.Decoys, given.NearOptimalRoutes, given.BruteForceExtraMoves);
        }
        return new DifficultyTarget(target.MinScore, target.MaxScore, minimums, target.MinTwistReliance);
    }

    private static void Require(bool holds, string path, string rule)
    {
        if (!holds)
        {
            throw Problem(path, rule);
        }
    }

    private static JsonException Problem(string path, string rule) => new($"The recipe file's \"{path}\" {rule}.");
}

internal sealed record RecipeFileJson
{
    public required StarLimitsJson StarLimits { get; init; }

    public required Dictionary<int, RecipeChapterJson> Chapters { get; init; }
}

internal sealed record StarLimitsJson
{
    public required int MinRoutes { get; init; }

    public required int MaxExtraMoves { get; init; }
}

internal sealed record RecipeChapterJson
{
    public required string Name { get; init; }

    public required bool Bends { get; init; }

    public required Dictionary<string, RecipeBandJson> Bands { get; init; }
}

internal sealed record RecipeBandJson
{
    public required int[] Sizes { get; init; }

    public required int[] Par { get; init; }

    // Required but nullable, so a band must say "null" to mean no twists
    public required RecipeTwistsJson? Twists { get; init; }

    public required int Candidates { get; init; }

    public required int? OneStarExtraMoves { get; init; }

    public required RecipeTargetJson Target { get; init; }
}

// Read into required properties first, so a missing density can't default to 0 (chunk 22's note on TwistDensities)
internal sealed record RecipeTwistsJson
{
    public required int WallPercent { get; init; }

    public required int StickyPercent { get; init; }

    public required int TrapPercent { get; init; }

    public required int TeleporterPercent { get; init; }
}

internal sealed record RecipeTargetJson
{
    public required int MinScore { get; init; }

    public required int MaxScore { get; init; }

    public RecipeMinimumsJson? Minimums { get; init; }

    public int MinTwistReliance { get; init; }
}

internal sealed record RecipeMinimumsJson
{
    public int? AwayMoves { get; init; }

    public int? GreedyGap { get; init; }

    public int? Decoys { get; init; }

    public long? NearOptimalRoutes { get; init; }

    public int? BruteForceExtraMoves { get; init; }
}

// Source-generated, as the game's parsers are
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(RecipeFileJson))]
internal partial class RecipeJsonContext : JsonSerializerContext;
