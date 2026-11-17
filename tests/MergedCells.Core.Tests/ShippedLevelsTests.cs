using AutoFixture;

namespace MergedCells.Core.Tests;

// The level files in levels/, linked into the test output
public class ShippedLevelsTests
{
    private readonly Fixture fixture = new();

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
    public void File_name_is_the_level_s_name(string file)
    {
        Assert.Equal(FileNameFor(Load(file).Name), file);
    }

    // "Over the Top" is over-the-top.json: lowercase, with each run of other characters a single hyphen
    private static string FileNameFor(string name) =>
        System.Text.RegularExpressions.Regex.Replace(name.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-') + ".json";

    // Sticky squares were dropped; a campaign copy and a share code both refuse one
    [Theory]
    [MemberData(nameof(LevelFiles))]
    public void Shipped_level_has_no_sticky_square(string file)
    {
        var map = Load(file).Map;

        Assert.DoesNotContain(
            Enumerable.Range(0, map.Rows).SelectMany(row => Enumerable.Range(0, map.Columns).Select(column => new Cell(row, column))),
            cell => map.TileAt(cell) == Tile.Sticky);
    }

    // Any level a player 3-stars can be copied, shared and imported as it is
    [Theory]
    [MemberData(nameof(LevelFiles))]
    public void Shipped_level_copies_and_survives_a_share_code(string file)
    {
        var level = Load(file);
        var copy = CustomLevelCopies.FromCampaign(level, fixture.Create<Guid>(), new DateTimeOffset(fixture.Create<DateTime>().Date, TimeSpan.Zero), 16);

        var read = ShareCode.Read(ShareCode.Write(copy)).Level;

        Assert.NotNull(read);
        Assert.True(RegionGrids.SameLevel(level.Map, read.Map));
    }

    [Theory]
    [MemberData(nameof(LevelFiles))]
    public void Stored_par_matches_the_solver(string file)
    {
        var level = Load(file);

        Assert.Equal(Solver.Solve(level.Map).Par, level.Par);
    }

    // A looping slide is a silent no-op in play, which a player can't tell from a broken swipe. Under a timeout, with the
    // slides on another thread, so a broken loop guard fails the test instead of hanging the run
    [Theory(Timeout = 10_000)]
    [MemberData(nameof(LevelFiles))]
    public async Task No_slide_from_a_reachable_stop_loops(string file)
    {
        var map = Load(file).Map;

        var solution = await Task.Run(() => Solver.Solve(map), TestContext.Current.CancellationToken);

        Assert.Equal(0, solution.LoopingSlides);
    }

    [Fact]
    public void No_two_levels_share_an_id()
    {
        var ids = Files().Select(file => Load(file).Id).ToArray();

        Assert.Equal(ids.Length, ids.Distinct().Count());
    }

    [Fact]
    public void There_are_at_most_a_hundred_and_fifty_levels()
    {
        Assert.InRange(Files().Length, 1, 150);
    }
}
