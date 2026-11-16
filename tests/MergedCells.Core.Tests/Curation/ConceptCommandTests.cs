using MergedCells.Curation;

namespace MergedCells.Core.Tests;

public sealed class ConceptCommandTests : IDisposable
{
    // "open" hits on every starting map; "top" asks for a score of 100, which no 6x6 map reaches
    private const string Bands =
        """{"open":{"sizes":[6,6],"par":[1,20],"twists":null,"candidates":2,"oneStarExtraMoves":null,"target":{"minScore":0,"maxScore":100}},"top":{"sizes":[6,6],"par":[1,20],"twists":null,"candidates":2,"oneStarExtraMoves":2,"target":{"minScore":100,"maxScore":100}}}""";

    private static readonly string Recipe =
        "{\"starLimits\":{\"minRoutes\":10,\"maxExtraMoves\":10},\"chapters\":{"
        + string.Join(",", Enumerable.Range(1, 4).Select(number => $"\"{number}\":{{\"name\":\"Recipe {number}\",\"bends\":false,\"bands\":{Bands}}}"))
        + "}}";

    // A wall line across row 2 with a gap at column 3, and the start somewhere in the top-left pair
    private static readonly string[] Blocks = [". . . . . .", ". . . . . .", ". . . . . .", ". . . . . .", ". . . . . .", ". . . . . E"];

    private static readonly string[] Tiles = ["s s . . . .", ". . . . . .", "# # # . # #", ". . . . . .", ". . . . . .", ". . . . . ."];

    private static readonly int[] WallColumns = [0, 1, 2, 4, 5];

    private readonly TempWorkspace temp = new();

    public ConceptCommandTests()
    {
        temp.WriteRecipes(Recipe);
        WriteConcept("gap", 1, "open");
    }

    public void Dispose() => temp.Dispose();

    private void WriteConcept(string name, int chapter, string band)
    {
        Directory.CreateDirectory(temp.Workspace.ConceptsFolder);
        static string Layer(string[] rows) => "[" + string.Join(", ", rows.Select(row => $"\"{row}\"")) + "]";
        File.WriteAllText(
            Path.Combine(temp.Workspace.ConceptsFolder, $"{name}.json"),
            $$"""{"chapter": {{chapter}}, "band": "{{band}}", "par": [1, 20], "bends": false, "twists": null, "maxWallRun": null, "candidates": 2, "blocks": {{Layer(Blocks)}}, "tiles": {{Layer(Tiles)}}}""");
    }

    private string Folder(string name = "gap") => Path.Combine(temp.Workspace.CandidatesFolder, $"concept-{name}-seed-1");

    private CommandResult Concept(params string[] args) => ConceptCommand.Run(temp.Workspace, args);

    private string[] Candidates() => Directory.GetFiles(Folder(), "c*.json").Order(StringComparer.Ordinal).ToArray();

    [Fact(Timeout = 30_000)]
    public async Task Writes_candidates_and_a_run_file()
    {
        var result = await Task.Run(() => Concept("gap"), TestContext.Current.CancellationToken);

        Assert.Equal(CommandResult.Done, result.ExitCode);
        Assert.Equal(new RunFile(1, "open", 1, null), RunFile.Parse(File.ReadAllText(Path.Combine(Folder(), "run.json"))));
        Assert.NotEmpty(Candidates());
    }

    [Fact(Timeout = 30_000)]
    public async Task Candidates_keep_the_fixed_walls()
    {
        await Task.Run(() => Concept("gap"), TestContext.Current.CancellationToken);

        Assert.All(Candidates(), file => Assert.All(
            WallColumns, column => Assert.Equal(Tile.Wall, LevelFile.Parse(File.ReadAllText(file)).Map.TileAt(new(2, column)))));
    }

    [Fact(Timeout = 30_000)]
    public async Task Promote_takes_a_concept_candidate()
    {
        await Task.Run(() => Concept("gap"), TestContext.Current.CancellationToken);

        var result = PromoteCommand.Run(temp.Workspace, [Candidates()[0], "--name", "Gap Level"]);

        Assert.Equal(CommandResult.Done, result.ExitCode);
    }

    [Fact(Timeout = 60_000)]
    public async Task Unreachable_target_reports_none_hit()
    {
        WriteConcept("high", 1, "top");

        var result = await Task.Run(() => Concept("high", "--count", "1"), TestContext.Current.CancellationToken);

        Assert.Equal(CommandResult.Done, result.ExitCode);
        Assert.Contains("None hit the target", result.Output, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(Folder("high"), "index.html")));
    }

    [Theory]
    [InlineData("nope", 0, "")]
    [InlineData("Bad_Name", 0, "")]
    [InlineData("far", 9, "open")]
    [InlineData("odd", 1, "nope")]
    public void Refused_and_writes_nothing(string name, int chapter, string band)
    {
        if (chapter > 0)
        {
            WriteConcept(name, chapter, band);
        }

        var result = Concept(name);

        Assert.Equal(CommandResult.Refused, result.ExitCode);
        Assert.False(Directory.Exists(temp.Workspace.CandidatesFolder));
    }

    [Theory]
    [InlineData]
    [InlineData("gap", "--seed")]
    [InlineData("gap", "--colour", "red")]
    public void Usage_mistakes_print_the_usage(params string[] args)
    {
        var result = Concept(args);

        Assert.Equal((CommandResult.UsageMistake, true), (result.ExitCode, result.Output.Contains(ConceptCommand.Usage, StringComparison.Ordinal)));
    }
}
