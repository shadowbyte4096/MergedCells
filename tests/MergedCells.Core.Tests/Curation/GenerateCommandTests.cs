using MergedCells.Curation;

namespace MergedCells.Core.Tests;

public sealed class GenerateCommandTests : IDisposable
{
    // Every climb hits a target of 0 to 100 on its starting map, so the command runs in moments
    private const string TinyRecipe =
        """{"starLimits":{"minRoutes":10,"maxExtraMoves":10},"chapters":{"1":{"name":"Plain sliding","bends":false,"bands":{"tiny":{"sizes":[5,5],"par":[2,4],"twists":null,"candidates":3,"oneStarExtraMoves":2,"target":{"minScore":0,"maxScore":100}}}}}}""";

    private readonly TempWorkspace temp = new();

    public GenerateCommandTests() => temp.WriteRecipes(TinyRecipe);

    public void Dispose() => temp.Dispose();

    private string Folder => Path.Combine(temp.Workspace.CandidatesFolder, "chapter-1-tiny-seed-1");

    private CommandResult Generate(params string[] args) => GenerateCommand.Run(temp.Workspace, args);

    private string[] Candidates() =>
        Directory.GetFiles(Folder, "c*.json").Select(path => Path.GetFileName(path)).Order(StringComparer.Ordinal).ToArray();

    private Dictionary<string, byte[]> Files() =>
        Directory.GetFiles(Folder).ToDictionary(path => Path.GetFileName(path), File.ReadAllBytes);

    [Fact(Timeout = 30_000)]
    public async Task Writes_the_candidates_run_file_page_and_gdignore()
    {
        var result = await Task.Run(() => Generate("1", "tiny"), TestContext.Current.CancellationToken);

        Assert.Equal(CommandResult.Done, result.ExitCode);
        Assert.Equal(["c1.json", "c2.json", "c3.json"], Candidates());
        Assert.True(File.Exists(Path.Combine(Folder, "run.json")));
        Assert.True(File.Exists(Path.Combine(Folder, "index.html")));
        Assert.True(File.Exists(Path.Combine(temp.Workspace.CandidatesFolder, ".gdignore")));
    }

    [Fact(Timeout = 30_000)]
    public async Task Candidates_are_level_files_numbered_from_1()
    {
        await Task.Run(() => Generate("1", "tiny"), TestContext.Current.CancellationToken);

        var level = LevelFile.Parse(File.ReadAllText(Path.Combine(Folder, "c2.json")));
        Assert.Equal((2, "Candidate 2", 1, Solver.Solve(level.Map).Par), (level.Id, level.Name, level.Chapter, (int?)level.Par));
    }

    [Fact(Timeout = 30_000)]
    public async Task Same_seed_gives_the_same_files()
    {
        var first = await Task.Run(() => { Generate("1", "tiny"); return Files(); }, TestContext.Current.CancellationToken);
        var second = await Task.Run(() => { Generate("1", "tiny"); return Files(); }, TestContext.Current.CancellationToken);

        Assert.Equal(first.Keys.Order(), second.Keys.Order());
        Assert.All(first, file => Assert.Equal(file.Value, second[file.Key]));
    }

    [Fact(Timeout = 30_000)]
    public async Task Rerun_with_fewer_hits_leaves_no_stale_candidates()
    {
        await Task.Run(() => Generate("1", "tiny", "--count", "3"), TestContext.Current.CancellationToken);

        await Task.Run(() => Generate("1", "tiny", "--count", "1"), TestContext.Current.CancellationToken);

        Assert.Equal(["c1.json"], Candidates());
    }

    [Fact(Timeout = 30_000)]
    public async Task Rerun_keeps_other_files_in_the_folder()
    {
        await Task.Run(() => Generate("1", "tiny"), TestContext.Current.CancellationToken);
        File.WriteAllText(Path.Combine(Folder, "notes.txt"), "keep me");

        await Task.Run(() => Generate("1", "tiny"), TestContext.Current.CancellationToken);

        Assert.True(File.Exists(Path.Combine(Folder, "notes.txt")));
    }

    [Theory]
    [InlineData("2", "tiny")]
    [InlineData("1", "huge")]
    public void Unknown_chapter_or_band_is_refused(string chapter, string band)
    {
        var result = Generate(chapter, band);

        Assert.Equal(CommandResult.Refused, result.ExitCode);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("1", "tiny", "--seed")]
    [InlineData("1", "tiny", "--count", "0")]
    [InlineData("one", "tiny")]
    [InlineData("1", "tiny", "--colour", "red")]
    public void Usage_mistakes_print_the_usage(params string[] args)
    {
        Assert.Equal(CommandResult.UsageMistake, Generate(args).ExitCode);
    }

    [Fact]
    public void Out_of_range_climb_number_is_refused_with_a_message()
    {
        var config = File.ReadAllText(temp.Workspace.ConfigFile);
        File.WriteAllText(temp.Workspace.ConfigFile, System.Text.RegularExpressions.Regex.Replace(config, "\"maxSteps\": \\d+", "\"maxSteps\": 0"));

        var result = Generate("1", "tiny");

        Assert.Equal((CommandResult.Refused, true), (result.ExitCode, result.Output.Contains("climb.maxSteps", StringComparison.Ordinal)));
    }

    [Fact]
    public void Missing_recipe_file_is_refused_with_a_message()
    {
        File.Delete(temp.Workspace.RecipesFile);

        Assert.Equal(CommandResult.Refused, Generate("1", "tiny").ExitCode);
    }

    [Fact]
    public void Run_file_round_trips()
    {
        var run = new RunFile(3, "medium", 7, null);

        Assert.Equal(run, RunFile.Parse(run.Write()));
    }
}
