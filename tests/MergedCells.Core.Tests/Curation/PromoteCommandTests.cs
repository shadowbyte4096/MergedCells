using MergedCells.Curation;

namespace MergedCells.Core.Tests;

// On TempWorkspace's own small campaign: chapters One [1, 2], Two [3, 6] and Three [4], next ID 7
public sealed class PromoteCommandTests : IDisposable
{
    private readonly TempWorkspace temp = new();

    public void Dispose() => temp.Dispose();

    private string Folder => Path.Combine(temp.Root, "builds", "candidates", "test");

    // A candidate file as generate would write it
    private string Candidate(int chapter, Map? map = null, int? oneStarExtraMoves = null)
    {
        Directory.CreateDirectory(Folder);
        map ??= TempWorkspace.ReachableMap();
        var path = Path.Combine(Folder, "c1.json");
        File.WriteAllText(path, LevelFile.Write(new Level(1, "Candidate 1", chapter, map, Solver.Solve(map).Par!.Value)));
        if (oneStarExtraMoves is { } extra)
        {
            File.WriteAllText(Path.Combine(Folder, "run.json"), new RunFile(chapter, "medium", 1, extra).Write());
        }
        return path;
    }

    private CommandResult Promote(string file, params string[] args) => PromoteCommand.Run(temp.Workspace, [file, .. args]);

    private CampaignFiles Files() => CampaignFiles.Load(temp.Workspace);

    private int[] ChapterIds(int chapter) => Files().Campaign.Chapters[chapter - 1].Levels.Select(level => level.Id).ToArray();

    [Fact]
    public void Writes_the_level_under_the_next_id()
    {
        Promote(Candidate(1), "--name", "Fresh Start");

        Assert.Equal(TempWorkspace.NextId, LevelFile.Parse(File.ReadAllText(Path.Combine(temp.Workspace.LevelsFolder, "fresh-start.json"))).Id);
    }

    [Fact]
    public void Places_the_level_at_the_chapter_s_end()
    {
        Promote(Candidate(2), "--name", "Fresh Start");

        Assert.Equal([3, 6, TempWorkspace.NextId], ChapterIds(2));
    }

    [Fact]
    public void Places_the_level_after_the_given_id()
    {
        Promote(Candidate(2), "--name", "Fresh Start", "--after", "3");

        Assert.Equal([3, TempWorkspace.NextId, 6], ChapterIds(2));
    }

    [Fact]
    public void Appends_a_new_chapter_with_the_recipe_s_name()
    {
        Promote(Candidate(TempWorkspace.ChapterCount + 1), "--name", "Fresh Start");

        var chapter = Files().Campaign.Chapters[^1];
        Assert.Equal(
            (TempWorkspace.ChapterCount + 1, $"Recipe {TempWorkspace.ChapterCount + 1}", 0, TempWorkspace.NextId),
            (chapter.Number, chapter.Name, chapter.Intro.Count, chapter.Levels.Single().Id));
    }

    [Fact]
    public void Adds_level_stars_for_a_medium_band()
    {
        Promote(Candidate(1, oneStarExtraMoves: 2), "--name", "Fresh Start");

        Assert.Equal(TempWorkspace.LevelTwoStars, Files().Config.Campaign.LevelStars[TempWorkspace.NextId]);
    }

    [Fact]
    public void Result_passes_the_campaign_and_range_checks()
    {
        Promote(Candidate(1, oneStarExtraMoves: 2), "--name", "Fresh Start");

        var files = Files();
        Assert.Null(Record.Exception(() => CampaignFiles.Check(files.Levels.Select(level => level.Level).ToArray(), files.ChaptersText, files.ConfigText)));
    }

    [Fact]
    public void Promote_into_chapter_1_reports_every_later_gate()
    {
        var output = Promote(Candidate(1), "--name", "Fresh Start").Output;

        Assert.All(Enumerable.Range(2, TempWorkspace.ChapterCount - 1), chapter => Assert.Contains($"Chapter {chapter}'s gate:", output, StringComparison.Ordinal));
    }

    [Fact]
    public void Promote_into_the_last_chapter_reports_no_gates()
    {
        var output = Promote(Candidate(TempWorkspace.ChapterCount), "--name", "Fresh Start").Output;

        Assert.Contains("No gates moved.", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Reads_a_candidate_with_crlf_and_a_byte_order_mark()
    {
        var path = Candidate(1);
        File.WriteAllText(path, (char)0xFEFF + File.ReadAllText(path).Replace("\n", "\r\n"));

        Promote(path, "--name", "Fresh Start");

        var written = File.ReadAllBytes(Path.Combine(temp.Workspace.LevelsFolder, "fresh-start.json"));
        Assert.Equal((false, (byte)'{'), (written.Contains((byte)'\r'), written[0]));
    }

    [Theory]
    [InlineData("Alpha", 1, true)]
    [InlineData("ALPHA", 1, true)]
    [InlineData("!!!", 1, true)]
    [InlineData(" ", 1, true)]
    [InlineData("Fresh Start", 1, true, "--after", "3")]
    [InlineData("Fresh Start", TempWorkspace.ChapterCount + 2, true)]
    [InlineData("Fresh Start", 1, false, "--band", "medium")]
    public void Refuses_and_leaves_every_file_unchanged(string name, int chapter, bool reachable, params string[] extra)
    {
        var path = Candidate(chapter, reachable ? TempWorkspace.ReachableMap() : TempWorkspace.UnreachableMap());
        var before = temp.Snapshot();

        var result = Promote(path, ["--name", name, .. extra]);

        Assert.Equal(CommandResult.Refused, result.ExitCode);
        Assert.Equal(before, temp.Snapshot());
    }

    [Fact]
    public void Refuses_a_stored_par_that_differs_from_the_solver_s()
    {
        var path = Candidate(1);
        File.WriteAllText(path, File.ReadAllText(path).Replace("\"par\": 4", "\"par\": 5"));

        Assert.Equal(CommandResult.Refused, Promote(path, "--name", "Fresh Start").ExitCode);
    }

    // The level file is written before chapters.json fails, so it must be taken back out
    [Fact]
    public void A_file_that_cannot_be_written_is_refused_and_changes_nothing()
    {
        var path = Candidate(1);
        var before = temp.Snapshot();
        File.SetAttributes(temp.Workspace.ChaptersFile, FileAttributes.ReadOnly);

        var result = Promote(path, "--name", "Fresh Start");

        Assert.Equal(CommandResult.Refused, result.ExitCode);
        Assert.Equal(before, temp.Snapshot());
    }

    [Fact]
    public void Warns_about_a_long_name_but_promotes()
    {
        var result = Promote(Candidate(1), "--name", "Thirteen Char");

        Assert.Equal(CommandResult.Done, result.ExitCode);
        Assert.Contains("over 12 characters", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void Missing_name_prints_the_usage()
    {
        Assert.Equal(CommandResult.UsageMistake, Promote(Candidate(1)).ExitCode);
    }
}
