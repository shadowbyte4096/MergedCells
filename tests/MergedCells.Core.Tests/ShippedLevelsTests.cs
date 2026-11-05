namespace MergedCells.Core.Tests;

// The level files in levels/, linked into the test output
public class ShippedLevelsTests
{
    private static readonly string Folder = Path.Combine(AppContext.BaseDirectory, "levels");

    private static string[] Files() =>
        Directory.Exists(Folder) ? Directory.GetFiles(Folder, "*.json").Select(path => Path.GetFileName(path)).ToArray() : [];

    private static Level Load(string file) => LevelFile.Parse(File.ReadAllText(Path.Combine(Folder, file)));

    public static TheoryData<string> LevelFiles => new(Files());

    [Theory]
    [MemberData(nameof(LevelFiles))]
    public void Level_file_parses(string file)
    {
        Assert.Null(Record.Exception(() => Load(file)));
    }

    [Theory]
    [MemberData(nameof(LevelFiles))]
    public void File_name_is_the_level_id(string file)
    {
        Assert.Equal(int.Parse(Path.GetFileNameWithoutExtension(file)), Load(file).Id);
    }

    [Theory]
    [MemberData(nameof(LevelFiles))]
    public void Stored_par_matches_the_solver(string file)
    {
        var level = Load(file);

        Assert.Equal(Solver.Solve(level.Map).Par, level.Par);
    }

    [Fact]
    public void No_two_levels_share_an_id()
    {
        var ids = Files().Select(file => Load(file).Id).ToArray();

        Assert.Equal(ids.Length, ids.Distinct().Count());
    }

    [Fact]
    public void There_are_five_to_ten_levels()
    {
        Assert.InRange(Files().Length, 5, 10);
    }
}
