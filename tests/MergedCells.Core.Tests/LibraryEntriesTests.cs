using AutoFixture;

namespace MergedCells.Core.Tests;

public class LibraryEntriesTests
{
    private static readonly Fixture Fixture = new();

    private static readonly CustomLevelsConfig Config = new()
    {
        NewSide = 7,
        MinSide = 2,
        MaxSide = 30,
        MaxNameLength = 16,
        UndoLimit = 5,
        Bands = [new DifficultyBand { Name = "Easy", MinScore = 0 }, new DifficultyBand { Name = "Hard", MinScore = 1 }],
    };

    private static readonly DifficultyRater Rater = new(DifficultyConfigs.With());

    private static CustomLevel Level(Map? map = null) =>
        new(Fixture.Create<Guid>(), "Ring Road", map ?? TestMaps.Example(), new DateTimeOffset(2026, 11, 20, 10, 0, 0, TimeSpan.Zero));

    // The row a publisher would send for this level
    private static LibraryRow Row(CustomLevel level)
    {
        var par = Solver.Solve(level.Map).Par ?? 1;
        return new LibraryRow(
            level.Id, ShareCode.Write(level), level.Name, LevelFingerprint.Of(level.Map, par), level.Map.Rows, level.Map.Columns, par,
            Fixture.Create<byte>() % 100, 12, 3, false, false, false);
    }

    public static TheoryData<string> Mismatches => ["code", "id", "name", "rows", "columns", "par", "layout", "no code"];

    [Theory]
    [MemberData(nameof(Mismatches))]
    public void Row_that_does_not_match_its_code_is_dropped(string what)
    {
        var row = Row(Level());
        var broken = what switch
        {
            "code" => row with { Code = "MC1-garbage" },
            "id" => row with { Id = Fixture.Create<Guid>() },
            "name" => row with { Name = "Other" },
            "rows" => row with { Rows = row.Rows + 1 },
            "columns" => row with { Columns = row.Columns + 1 },
            "par" => row with { Par = row.Par + 1 },
            "layout" => row with { Layout = "0123456789abcdef" },
            _ => row with { Code = null },
        };

        Assert.Null(LibraryEntries.Check(broken, Rater, Config));
    }

    [Fact]
    public void Looping_level_is_dropped()
    {
        var level = Level(TestMaps.LoopingLane());
        var row = Row(level) with { Par = 1, Layout = LevelFingerprint.Of(level.Map, 1) };

        Assert.Null(LibraryEntries.Check(row, Rater, Config));
    }

    [Fact]
    public void Unwinnable_level_is_dropped()
    {
        var level = Level(Map.Create(TestMaps.Grid("ABZ"), new(0, 0), new(0, 2), TestMaps.Walls(1, 3, (0, 1))));
        var row = Row(level) with { Par = 1, Layout = LevelFingerprint.Of(level.Map, 1) };

        Assert.Null(LibraryEntries.Check(row, Rater, Config));
    }

    [Fact]
    public void Level_over_the_max_side_is_dropped()
    {
        var wide = Map.Create(new int[2, Config.MaxSide + 1].Fill(), new(0, 0), new(1, Config.MaxSide));

        Assert.Null(LibraryEntries.Check(Row(Level(wide)), Rater, Config));
    }

    [Fact]
    public void Good_row_keeps_its_counts_and_flags()
    {
        var level = Level();
        var row = Row(level) with { Mine = true, Hidden = true };

        var entry = LibraryEntries.Check(row, Rater, Config)!;

        Assert.Equal((level.Id, level.Name, 12, 3, true, true, false), (entry.Id, entry.Level!.Name, entry.Finished, entry.Likes, entry.Mine, entry.Hidden, entry.Removed));
    }

    // The publisher's score only filters: each phone rates the level itself
    [Fact]
    public void Good_row_shows_the_phone_s_own_band()
    {
        var level = Level();
        var own = LevelCheck.Of(level.Map, Rater, Config);

        var entry = LibraryEntries.Check(Row(level) with { Score = own.Score == 0 ? 99 : 0 }, Rater, Config)!;

        Assert.Equal(own.Band, entry.Status!.Band);
    }

    [Fact]
    public void Tombstone_gives_an_entry_with_no_level()
    {
        var id = Fixture.Create<Guid>();
        var row = new LibraryRow(id, null, null, null, 5, 5, 4, 10, 0, 0, true, false, true);

        var entry = LibraryEntries.Check(row, Rater, Config)!;

        Assert.Equal((id, (CustomLevel?)null, true), (entry.Id, entry.Level, entry.Removed));
    }

    // Two pages can overlap while counts change under Most played (Review Focus 2)
    [Fact]
    public void Unseen_drops_rows_already_shown_and_repeats_within_the_page()
    {
        var shown = Row(Level());
        var fresh = Row(Level());

        var unseen = LibraryEntries.Unseen(new HashSet<Guid> { shown.Id }, [shown, fresh, fresh]);

        Assert.Equal([fresh.Id], unseen.Select(row => row.Id));
    }
}
