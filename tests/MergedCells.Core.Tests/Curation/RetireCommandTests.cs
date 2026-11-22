using System.Globalization;
using MergedCells.Curation;

namespace MergedCells.Core.Tests;

// On TempWorkspace's own small campaign: chapters One [1, 2], Two [3, 6] and Three [4]; level 2 has its own star limits
public sealed class RetireCommandTests : IDisposable
{
    private readonly TempWorkspace temp = new();

    public void Dispose() => temp.Dispose();

    private CommandResult Retire(int id) => RetireCommand.Run(temp.Workspace, [id.ToString(CultureInfo.InvariantCulture)]);

    private CampaignFiles Files() => CampaignFiles.Load(temp.Workspace);

    [Fact]
    public void Removes_the_level_from_its_chapter()
    {
        Retire(2);

        Assert.Equal([1], Files().Campaign.Chapters[0].Levels.Select(level => level.Id));
    }

    [Fact]
    public void Removes_its_level_stars_entry()
    {
        Retire(2);

        Assert.DoesNotContain(2, Files().Config.Campaign.LevelStars.Keys);
    }

    [Fact]
    public void Adds_its_id_to_the_retired_list()
    {
        Retire(2);

        Assert.Contains(2, Files().Retired);
    }

    [Fact]
    public void Retire_rewrites_the_server_level_list_without_the_level()
    {
        Retire(2);

        var levels = Files().Levels.Select(onDisk => onDisk.Level).ToArray();
        Assert.Equal(
            (ServerLevels.Write(levels), false),
            (File.ReadAllText(temp.Workspace.ServerLevelsFile), levels.Any(level => level.Id == 2)));
    }

    [Fact]
    public void Backs_the_file_up_and_removes_it_from_levels()
    {
        Retire(2);

        Assert.Equal(
            (false, true),
            (File.Exists(Path.Combine(temp.Workspace.LevelsFolder, "beta.json")), File.Exists(Path.Combine(temp.Workspace.RetiredBackupFolder, "2-beta.json"))));
    }

    [Fact]
    public void Refuses_a_chapter_s_only_level()
    {
        var before = temp.Snapshot();

        var result = Retire(4);

        Assert.Equal(CommandResult.Refused, result.ExitCode);
        Assert.Equal(before, temp.Snapshot());
    }

    [Fact]
    public void Refuses_an_id_with_no_level()
    {
        Assert.Equal(CommandResult.Refused, Retire(5).ExitCode);
    }

    // A folder where the backup should go makes the move fail before anything is written
    [Fact]
    public void A_backup_that_cannot_be_written_is_refused_and_changes_nothing()
    {
        Directory.CreateDirectory(Path.Combine(temp.Workspace.RetiredBackupFolder, "2-beta.json"));
        var before = temp.Snapshot();

        var result = Retire(2);

        Assert.Equal(CommandResult.Refused, result.ExitCode);
        Assert.Equal(before, temp.Snapshot());
    }

    // The level file is moved out first, so it must come back when a text fails to write
    [Fact]
    public void A_file_that_cannot_be_written_is_refused_and_changes_nothing()
    {
        var before = temp.Snapshot();
        File.SetAttributes(temp.Workspace.RetiredFile, FileAttributes.ReadOnly);

        var result = Retire(2);

        Assert.Equal(CommandResult.Refused, result.ExitCode);
        Assert.Equal(before, temp.Snapshot());
    }

    [Fact]
    public void Next_promote_after_retiring_the_block_s_highest_id_skips_it()
    {
        PromoteIntoChapter3("first.json", "First Try");
        Retire(TempWorkspace.NextId(3));

        PromoteIntoChapter3("second.json", "Fresh Start");

        Assert.Equal(TempWorkspace.NextId(3) + 1, Files().Levels.Single(level => level.Level.Name == "Fresh Start").Level.Id);
    }

    private void PromoteIntoChapter3(string fileName, string name)
    {
        var map = TempWorkspace.ReachableMap();
        var candidate = Path.Combine(temp.Root, fileName);
        File.WriteAllText(candidate, LevelFile.Write(new Level(1, "Candidate 1", 3, map, Solver.Solve(map).Par!.Value)));
        PromoteCommand.Run(temp.Workspace, [candidate, "--name", name]);
    }

    [Fact]
    public void Reports_the_gates_that_moved()
    {
        Assert.Contains("Chapter 2's gate:", Retire(2).Output, StringComparison.Ordinal);
    }

    [Fact]
    public void Missing_id_prints_the_usage()
    {
        Assert.Equal(CommandResult.UsageMistake, RetireCommand.Run(temp.Workspace, []).ExitCode);
    }
}
