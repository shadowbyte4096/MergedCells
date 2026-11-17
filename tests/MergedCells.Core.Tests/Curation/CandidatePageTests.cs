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
        var climbed = new ClimbedMap(map, Solver.Solve(map), metrics, rater.Score(metrics, DifficultyMode.Campaign), TwistReliance.Of(map, rater), 0);
        return new PageCandidate(
            $"c{number}.json", climbed, StarLimits.Reachable(map, 10, 10, 2), $"promote builds/candidates/c{number}.json --name \"NAME\"",
            TwistChecks.Of(map, metrics, rater));
    }

    // One card for a hand-made map, with its traps and pairs measured as generate measures them
    private static string PageOf(Map map)
    {
        var rater = new DifficultyRater(DifficultyConfigs.With());
        var metrics = rater.Measure(map)!;
        var climbed = new ClimbedMap(map, Solver.Solve(map), metrics, rater.Score(metrics, DifficultyMode.Campaign), null, 0);
        var candidate = new PageCandidate("c1.json", climbed, null, "promote c1.json", TwistChecks.Of(map, metrics, rater));
        return CandidatePage.Write(1, "hard", Band, 1, 1, [], [candidate]);
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

    private static readonly PageConcept Gap = new("the-gap", 1, ["A A E", ". . .", ". . ."], ["S . -", "# . #", ". . ."]);

    [Theory(Timeout = 10_000)]
    [AutoData]
    public async Task Concept_page_names_the_concept_in_its_heading(ulong seed)
    {
        var page = await Task.Run(() => CandidatePage.Write(3, "medium", Band, 1, 2, [], [Candidate(seed, 1)], Gap), TestContext.Current.CancellationToken);

        Assert.Contains("<h1>Concept the-gap · chapter 3 · medium · seed 1</h1>", page, StringComparison.Ordinal);
    }

    [Theory(Timeout = 10_000)]
    [AutoData]
    public async Task Concept_page_shows_both_layers(ulong seed)
    {
        var page = await Task.Run(() => CandidatePage.Write(3, "medium", Band, 1, 2, [], [Candidate(seed, 1)], Gap), TestContext.Current.CancellationToken);

        Assert.All(["S . -\n# . #", "A A E\n. . ."], layers => Assert.Contains(layers, page, StringComparison.Ordinal));
    }

    [Theory(Timeout = 10_000)]
    [AutoData]
    public async Task Card_shows_wall_stops_for_a_map_with_walls(ulong seed)
    {
        var page = await Task.Run(() => Page(seed), TestContext.Current.CancellationToken);

        Assert.Equal(2, Regex.Count(page, "<td>Wall stops on the best route</td>"));
    }

    // The traps chapter's maps have tiles but no walls, so the row would always read 0 there
    [Fact]
    public void Card_has_no_wall_stops_row_for_a_map_with_traps_but_no_walls()
    {
        Assert.DoesNotContain("Wall stops", PageOf(TestMaps.TrappedLane()), StringComparison.Ordinal);
    }

    [Fact]
    public void Card_has_no_wall_stops_row_for_a_plain_map()
    {
        Assert.DoesNotContain("Wall stops", PageOf(TestMaps.Example()), StringComparison.Ordinal);
    }

    [Fact]
    public void Card_names_the_traps_that_dont_matter()
    {
        Assert.Contains(
            "<td>Traps that matter</td><td>0 of 2 (not row 1, column 3; row 1, column 4)</td>", PageOf(TestMaps.CoveringTraps()), StringComparison.Ordinal);
    }

    [Fact]
    public void Card_shows_all_traps_matter_without_a_list()
    {
        Assert.Contains("<td>Traps that matter</td><td>1 of 1</td>", PageOf(TestMaps.TrapTutorial()), StringComparison.Ordinal);
    }

    [Fact]
    public void Card_has_no_traps_row_for_a_map_without_traps()
    {
        Assert.DoesNotContain("Traps that matter", PageOf(TestMaps.Example()), StringComparison.Ordinal);
    }

    [Fact]
    public void Card_names_the_pairs_that_dont_matter_by_letter()
    {
        Assert.Contains("<td>Pairs that matter</td><td>1 of 2 (not β)</td>", PageOf(TestMaps.IdleSecondPair()), StringComparison.Ordinal);
    }

    // Its pairs are 2 and 1 squares apart
    [Fact]
    public void Card_shows_the_average_pair_distance()
    {
        Assert.Contains("<td>Average pair distance</td><td>1.5</td>", PageOf(TestMaps.IdleSecondPair()), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Pairs that matter")]
    [InlineData("Average pair distance")]
    public void Card_has_no_pair_rows_for_a_map_without_pairs(string row)
    {
        Assert.DoesNotContain(row, PageOf(TestMaps.Example()), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("<td>Traps that matter</td><td>0 of 2")]
    [InlineData("<td>Pairs that matter</td><td>1 of 1</td>")]
    public void Card_shows_both_twists_rows_for_a_map_with_traps_and_pairs(string row)
    {
        Assert.Contains(row, PageOf(TestMaps.CoveringTrapsBesideAPair()), StringComparison.Ordinal);
    }
}
