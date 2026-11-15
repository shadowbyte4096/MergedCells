using System.Text.RegularExpressions;
using AutoFixture.Xunit3;
using MergedCells.Curation;

namespace MergedCells.Core.Tests;

// A smoke test of the page's content, not of its look
public class CandidatePageTests
{
    private static readonly BandRecipe Band = new(8, 8, 1, 100, GeneratedMaps.EveryTwist, 2, 2, new DifficultyTarget(0, 100));

    private static PageCandidate Candidate(ulong seed, int number)
    {
        var map = GeneratedMaps.Make(new MapRequest(8, 1, 100, Twists: GeneratedMaps.EveryTwist), seed + (ulong)number);
        var rater = new DifficultyRater(DifficultyConfigs.With());
        var metrics = rater.Measure(map)!;
        var climbed = new ClimbedMap(map, Solver.Solve(map), metrics, rater.Score(metrics, DifficultyMode.Campaign), TwistReliance.Of(map), 0);
        return new PageCandidate($"c{number}.json", climbed, StarLimits.Reachable(map, 10, 10, 2), $"promote builds/candidates/c{number}.json --name \"NAME\"");
    }

    private static string Page(ulong seed, string shippedName = "Top Shelf")
    {
        PageLevel[] shipped = [new(new Level(18, shippedName, 1, TestMaps.Example(), 3), 12)];
        return CandidatePage.Write(1, "hard", Band, 1, 2, shipped, [Candidate(seed, 1), Candidate(seed, 2)]);
    }

    [Theory(Timeout = 10_000)]
    [AutoData]
    public async Task Page_holds_every_candidate_s_promote_command(ulong seed)
    {
        var page = await Task.Run(() => Page(seed), TestContext.Current.CancellationToken);

        Assert.All(["c1.json", "c2.json"], file => Assert.Contains($"promote builds/candidates/{file} --name &quot;NAME&quot;", page, StringComparison.Ordinal));
    }

    [Theory(Timeout = 10_000)]
    [AutoData]
    public async Task Page_has_one_svg_per_map(ulong seed)
    {
        var page = await Task.Run(() => Page(seed), TestContext.Current.CancellationToken);

        Assert.Equal(3, Regex.Count(page, "<svg "));
    }

    [Theory(Timeout = 10_000)]
    [AutoData]
    public async Task Page_has_no_inline_style(ulong seed)
    {
        var page = await Task.Run(() => Page(seed), TestContext.Current.CancellationToken);

        Assert.DoesNotContain("style=", page, StringComparison.Ordinal);
    }

    [Theory(Timeout = 10_000)]
    [AutoData]
    public async Task Level_names_are_html_encoded(ulong seed)
    {
        var page = await Task.Run(() => Page(seed, "<Odd>"), TestContext.Current.CancellationToken);

        Assert.Contains("&lt;Odd&gt;", page, StringComparison.Ordinal);
    }
}
