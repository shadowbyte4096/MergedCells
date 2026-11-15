using MergedCells.Curation;

namespace MergedCells.Core.Tests;

public sealed class WorkspaceTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "mergedcells-workspace-" + Guid.NewGuid().ToString("N"));

    public WorkspaceTests() => Directory.CreateDirectory(folder);

    public void Dispose() => Directory.Delete(folder, recursive: true);

    [Fact]
    public void Find_root_walks_up_to_the_solution_folder()
    {
        File.WriteAllText(Path.Combine(folder, "MergedCells.sln"), "");
        var nested = Directory.CreateDirectory(Path.Combine(folder, "levels", "deeper")).FullName;

        Assert.Equal(Path.GetFullPath(folder), Workspace.FindRoot(nested));
    }

    [Fact]
    public void Find_root_is_null_without_a_solution()
    {
        Assert.Null(Workspace.FindRoot(folder));
    }

    [Fact]
    public void Help_lists_every_command()
    {
        var output = Program.Run(["help"], folder).Output;

        Assert.All(["generate", "promote", "retire", "help"], command => Assert.Contains(command, output, StringComparison.Ordinal));
    }

    [Fact]
    public void Unknown_command_prints_the_usage()
    {
        Assert.Equal(2, Program.Run(["frobnicate"], folder).ExitCode);
    }
}
