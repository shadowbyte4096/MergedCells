using MergedCells.Curation;

namespace MergedCells.Core.Tests;

public sealed class ServerLevelsTests : IDisposable
{
    private readonly TempWorkspace temp = new();

    public void Dispose() => temp.Dispose();

    private static Level Level(int id) => new(id, $"Level {id}", 1, TestMaps.Example(), 3);

    [Fact]
    public void Rows_are_in_id_order_with_fingerprint_and_par()
    {
        var text = ServerLevels.Write([Level(6), Level(1), Level(3)]);

        var rows = new[] { 1, 3, 6 }.Select(id => text.IndexOf($"({id}, '{LevelFingerprint.Of(Level(id))}', 3)", StringComparison.Ordinal)).ToArray();
        Assert.True(rows.All(at => at >= 0) && rows[0] < rows[1] && rows[1] < rows[2], text);
    }

    [Fact]
    public void Text_inserts_into_the_level_table()
    {
        Assert.Contains("insert into game.levels (id, layout, par) values\n", ServerLevels.Write([Level(1)]), StringComparison.Ordinal);
    }

    [Fact]
    public void Text_ends_with_on_conflict_do_nothing()
    {
        Assert.EndsWith("\non conflict (id, layout) do nothing;\n", ServerLevels.Write([Level(1), Level(2)]), StringComparison.Ordinal);
    }

    [Fact]
    public void Server_levels_command_writes_the_list()
    {
        var result = ServerLevelsCommand.Run(temp.Workspace, []);

        var levels = CampaignFiles.Load(temp.Workspace).Levels.Select(onDisk => onDisk.Level);
        Assert.Equal((CommandResult.Done, ServerLevels.Write(levels)), (result.ExitCode, File.ReadAllText(temp.Workspace.ServerLevelsFile)));
    }
}
