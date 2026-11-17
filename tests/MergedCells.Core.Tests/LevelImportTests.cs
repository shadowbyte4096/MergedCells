using AutoFixture;

namespace MergedCells.Core.Tests;

public class LevelImportTests
{
    private readonly Fixture fixture = new();

    private static readonly CustomLevelsConfig Config = new()
    {
        NewSide = 7,
        MinSide = 2,
        MaxSide = 30,
        MaxNameLength = 16,
        UndoLimit = 5,
        Bands = [new DifficultyBand { Name = "Easy", MinScore = 0 }],
    };

    private static readonly DateTimeOffset Then = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);

    private static readonly IReadOnlySet<Guid> NoneShared = new HashSet<Guid>();

    // Imports happen at a time with a fraction of a second, which the file can't keep
    private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 11, 17, 12, 0, 0, TimeSpan.Zero).AddMilliseconds(250);

    private static readonly DateTimeOffset NowInSeconds = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);

    private CustomLevel Level(Map? map = null, DateTimeOffset? edited = null, DateTimeOffset? imported = null) =>
        new(fixture.Create<Guid>(), fixture.Create<string>(), map ?? TestMaps.Example(), edited ?? Then, imported);

    private static CustomLevelLoad Load(params CustomLevel[] levels) => new(levels, [], new HashSet<Guid>());

    private static ImportResult Import(CustomLevel coded, CustomLevelLoad load, IReadOnlySet<Guid>? shared = null) =>
        LevelImport.Of(ShareCode.Write(coded), load, shared ?? NoneShared, Config, Now);

    [Fact]
    public void New_code_is_added_with_the_import_time()
    {
        var result = Import(Level(), Load());

        Assert.Equal((ImportOutcome.Added, (DateTimeOffset?)NowInSeconds), (result.Outcome, result.Level?.Imported));
    }

    [Fact]
    public void Newer_edit_of_an_imported_level_updates_it()
    {
        var held = Level(imported: Then);
        var newer = held with { Edited = Then.AddDays(1), Imported = null };

        var result = Import(newer, Load(held));

        Assert.Equal((ImportOutcome.Updated, (DateTimeOffset?)Then.AddDays(1), (DateTimeOffset?)NowInSeconds), (result.Outcome, result.Level?.Edited, result.Level?.Imported));
    }

    [Fact]
    public void Second_import_of_the_same_code_is_same()
    {
        var held = Level(imported: Then);

        Assert.Equal(new ImportResult(ImportOutcome.Same, null), Import(held with { Imported = null }, Load(held)));
    }

    [Fact]
    public void Older_edit_is_have_newer()
    {
        var held = Level(imported: Then);

        Assert.Equal(new ImportResult(ImportOutcome.HaveNewer, null), Import(held with { Edited = Then.AddDays(-1), Imported = null }, Load(held)));
    }

    // Even a newer code: the player's own level only changes in their editor
    [Fact]
    public void Own_level_in_the_list_is_yours_already()
    {
        var own = Level();

        Assert.Equal(new ImportResult(ImportOutcome.YoursAlready, null), Import(own with { Edited = Then.AddDays(1) }, Load(own)));
    }

    [Fact]
    public void Shared_level_no_longer_in_the_list_is_restored_as_own()
    {
        var shared = Level();

        var result = Import(shared, Load(), new HashSet<Guid> { shared.Id });

        Assert.Equal((ImportOutcome.Restored, false), (result.Outcome, result.Level?.IsImported));
    }

    // A file this build skipped (newer or broken) must never be written over
    [Fact]
    public void Unreadable_id_is_refused()
    {
        var coded = Level();

        Assert.Equal(new ImportResult(ImportOutcome.Unreadable, null), Import(coded, new CustomLevelLoad([], [], new HashSet<Guid> { coded.Id })));
    }

    // A folder that couldn't be listed may hold this ID's file, so nothing is written
    [Fact]
    public void Unlisted_folder_refuses_the_import()
    {
        var unlisted = new CustomLevelLoad([], ["the folder can't be listed"], new HashSet<Guid>(), Listed: false);

        Assert.Equal(new ImportResult(ImportOutcome.FolderUnreadable, null), Import(Level(), unlisted));
    }

    [Fact]
    public void Side_over_max_is_too_big()
    {
        var wide = Map.Create(new int[2, Config.MaxSide + 1].Fill(), new(0, 0), new(1, Config.MaxSide));

        Assert.Equal(new ImportResult(ImportOutcome.TooBig, null), Import(Level(wide), Load()));
    }

    // Copies of campaign levels can be below the editor's minSide (chapter 1's Slide is 1×5)
    [Fact]
    public void One_by_five_level_imports()
    {
        var slide = Map.Create(TestMaps.Grid("ABCDE"), new(0, 0), new(0, 4));

        Assert.Equal(ImportOutcome.Added, Import(Level(slide), Load()).Outcome);
    }

    [Fact]
    public void Unwinnable_level_can_t_be_won()
    {
        var walled = Map.Create(TestMaps.Grid("ABZ"), new(0, 0), new(0, 2), TestMaps.Walls(1, 3, (0, 1)));

        Assert.Equal(new ImportResult(ImportOutcome.CantBeWon, null), Import(Level(walled), Load()));
    }

    [Fact]
    public void Looping_level_loops()
    {
        Assert.Equal(new ImportResult(ImportOutcome.Loops, null), Import(Level(TestMaps.LoopingLane()), Load()));
    }

    public static TheoryData<string, ImportOutcome> Problems => new()
    {
        { "nothing here", ImportOutcome.NoCode },
        { ShareCodeTests.PinnedCode[..^4], ImportOutcome.Damaged },
        { "MC2-AAAA", ImportOutcome.NewerGame },
    };

    [Theory]
    [MemberData(nameof(Problems))]
    public void Code_problems_pass_through(string text, ImportOutcome expected)
    {
        Assert.Equal(new ImportResult(expected, null), LevelImport.Of(text, Load(), NoneShared, Config, Now));
    }

    public static TheoryData<ImportOutcome, string> Lines => new()
    {
        { ImportOutcome.Added, "It's at the top of your list." },
        { ImportOutcome.Updated, "Replaced with the newer version." },
        { ImportOutcome.Restored, "One of your levels, back in your list." },
        { ImportOutcome.Same, "You already have this version." },
        { ImportOutcome.HaveNewer, "You already have a newer version." },
        { ImportOutcome.YoursAlready, "That's one of your own levels." },
        { ImportOutcome.NoCode, "Copy a level code, then tap Import." },
        { ImportOutcome.Damaged, "That code is damaged. It may have been cut short." },
        { ImportOutcome.NewerGame, "This level needs a newer version of Merged Cells." },
        { ImportOutcome.TooBig, "This level is bigger than 30×30." },
        { ImportOutcome.CantBeWon, "This level has no way to the end." },
        { ImportOutcome.Loops, "A slide in this level loops forever." },
        { ImportOutcome.Unreadable, "A level with this ID is here but can't be read." },
        { ImportOutcome.NotSaved, "This level couldn't be saved." },
        { ImportOutcome.FolderUnreadable, "Your levels can't be read right now, so nothing was imported." },
    };

    // A write that fails mustn't leave the sheet claiming the level is in the list, or offering to play it
    [Fact]
    public void Not_saved_drops_the_level()
    {
        Assert.Equal(new ImportResult(ImportOutcome.NotSaved, null), new ImportResult(ImportOutcome.Added, Level()).NotSaved());
    }

    [Theory]
    [MemberData(nameof(Lines))]
    public void Each_outcome_has_its_line(ImportOutcome outcome, string line)
    {
        Assert.Equal(line, new ImportResult(outcome, null).Describe(Config.MaxSide));
    }

    [Fact]
    public void Every_outcome_has_a_title()
    {
        Assert.All(Enum.GetValues<ImportOutcome>(), outcome => Assert.False(string.IsNullOrWhiteSpace(new ImportResult(outcome, null).Title)));
    }
}

internal static class GridFill
{
    // Every square its own region
    internal static int[,] Fill(this int[,] grid)
    {
        for (var row = 0; row < grid.GetLength(0); row++)
        {
            for (var column = 0; column < grid.GetLength(1); column++)
            {
                grid[row, column] = row * grid.GetLength(1) + column + 1;
            }
        }
        return grid;
    }
}
