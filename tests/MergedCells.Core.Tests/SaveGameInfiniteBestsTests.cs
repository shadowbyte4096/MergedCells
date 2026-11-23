using System.Text.Json;
using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

public class SaveGameInfiniteBestsTests
{
    // Any generated numbers, made into a choice the save accepts
    private static InfiniteChoice ChoiceOf(byte difficulty, byte side, bool walls, bool traps, bool teleporters, bool irregular) =>
        new(1 + difficulty % InfiniteConfig.DifficultyCount, 2 + side, walls, traps, teleporters, irregular);

    private static InfiniteBest BestOf(ushort streak, uint run, ushort efficiency) => new(streak, run, efficiency % 10001);

    [Theory]
    [AutoData]
    public void No_entry_reads_as_null(byte difficulty, byte side, bool walls)
    {
        Assert.Null(new SaveGame().InfiniteBestFor(ChoiceOf(difficulty, side, walls, false, false, false)));
    }

    [Theory]
    [AutoData]
    public void Bests_round_trip(byte difficulty, byte side, bool walls, bool traps, bool teleporters, bool irregular, ushort streak, uint run, ushort efficiency)
    {
        var choice = ChoiceOf(difficulty, side, walls, traps, teleporters, irregular);
        var save = new SaveGame();
        save.RecordInfiniteBest(choice, BestOf(streak, run, efficiency));

        Assert.Equal(save.InfiniteBestFor(choice), SaveGame.Parse(save.ToJson()).InfiniteBestFor(choice));
    }

    [Theory]
    [AutoData]
    public void Null_efficiency_round_trips(byte difficulty, byte side, ushort streak, uint run)
    {
        var choice = ChoiceOf(difficulty, side, false, false, false, false);
        var save = new SaveGame();
        save.RecordInfiniteBest(choice, new InfiniteBest(streak + 1, run, null));

        Assert.Null(SaveGame.Parse(save.ToJson()).InfiniteBestFor(choice)!.Efficiency);
    }

    [Fact]
    public void Entries_are_written_in_choice_order()
    {
        var choices = new[]
        {
            new InfiniteChoice(3, 10, true, false, false, false),
            new InfiniteChoice(2, 12, false, false, false, false),
            new InfiniteChoice(3, 10, false, false, false, true),
            new InfiniteChoice(3, 8, true, true, true, true),
            new InfiniteChoice(3, 10, false, true, false, false),
        };
        var save = new SaveGame();
        foreach (var choice in choices)
        {
            save.RecordInfiniteBest(choice, new InfiniteBest(1, 1, null));
        }

        var written = JsonDocument.Parse(save.ToJson()).RootElement.GetProperty("infiniteBests").EnumerateArray()
            .Select(entry => (
                entry.GetProperty("difficulty").GetInt32(),
                entry.GetProperty("side").GetInt32(),
                entry.GetProperty("walls").GetBoolean(),
                entry.GetProperty("traps").GetBoolean(),
                entry.GetProperty("teleporters").GetBoolean(),
                entry.GetProperty("irregular").GetBoolean()))
            .ToArray();
        Assert.Equal(
            [(2, 12, false, false, false, false), (3, 8, true, true, true, true), (3, 10, false, false, false, true), (3, 10, false, true, false, false), (3, 10, true, false, false, false)],
            written);
    }

    public static TheoryData<InfiniteBest, InfiniteBest> Raises => new()
    {
        { new InfiniteBest(8, 3410, 9400), new InfiniteBest(8, 3410, 9400) },
        { new InfiniteBest(0, 5000, null), new InfiniteBest(7, 5000, 9400) },
        { new InfiniteBest(0, 0, 9500), new InfiniteBest(7, 3410, 9500) },
    };

    [Theory]
    [MemberData(nameof(Raises))]
    public void Record_raises_each_field_alone(InfiniteBest candidate, InfiniteBest expected)
    {
        var choice = new InfiniteChoice(4, 12, true, false, true, false);
        var save = new SaveGame();
        save.RecordInfiniteBest(choice, new InfiniteBest(7, 3410, 9400));

        var raised = save.RecordInfiniteBest(choice, candidate);

        Assert.Equal((true, expected), (raised, save.InfiniteBestFor(choice)));
    }

    [Theory]
    [AutoData]
    public void Lower_candidate_changes_nothing(byte difficulty, byte side, ushort streak, uint run, ushort efficiency)
    {
        var choice = ChoiceOf(difficulty, side, true, false, false, false);
        var save = new SaveGame();
        var saved = new InfiniteBest(streak + 1, run + 1L, efficiency % 10000 + 1);
        save.RecordInfiniteBest(choice, saved);

        var raised = save.RecordInfiniteBest(choice, new InfiniteBest(streak, run, efficiency % 10000));

        Assert.Equal((false, saved), (raised, save.InfiniteBestFor(choice)));
    }

    [Theory]
    [AutoData]
    public void Candidate_of_zeros_raises_nothing(byte difficulty, byte side)
    {
        var choice = ChoiceOf(difficulty, side, false, false, false, false);
        var save = new SaveGame();

        var raised = save.RecordInfiniteBest(choice, InfiniteBest.None);

        Assert.Equal((false, (InfiniteBest?)null), (raised, save.InfiniteBestFor(choice)));
    }

    // Ten give-ups in a row complete the window at 0%: nothing was solved, so there's still no best to show
    [Theory]
    [AutoData]
    public void All_zero_candidate_leaves_no_entry(byte difficulty, byte side)
    {
        var choice = ChoiceOf(difficulty, side, false, false, false, false);
        var save = new SaveGame();

        var raised = save.RecordInfiniteBest(choice, new InfiniteBest(0, 0, 0));

        Assert.Equal((false, (InfiniteBest?)null), (raised, save.InfiniteBestFor(choice)));
    }

    [Theory]
    [AutoData]
    public void Zero_efficiency_still_joins_a_real_best(byte difficulty, byte side, ushort streak, uint run)
    {
        var choice = ChoiceOf(difficulty, side, false, false, false, false);
        var save = new SaveGame();
        var saved = new InfiniteBest(streak + 1, run, null);
        save.RecordInfiniteBest(choice, saved);

        save.RecordInfiniteBest(choice, new InfiniteBest(0, 0, 0));

        Assert.Equal(saved with { Efficiency = 0 }, save.InfiniteBestFor(choice));
    }

    [Theory]
    [AutoData]
    public void Null_efficiency_never_lowers_a_saved_one(byte difficulty, byte side, ushort streak, uint run, ushort efficiency)
    {
        var choice = ChoiceOf(difficulty, side, true, false, true, false);
        var save = new SaveGame();
        var saved = BestOf(streak, run, efficiency);
        save.RecordInfiniteBest(choice, saved);

        save.RecordInfiniteBest(choice, saved with { Streak = saved.Streak + 1, Efficiency = null });

        Assert.Equal(saved with { Streak = saved.Streak + 1 }, save.InfiniteBestFor(choice));
    }

    [Theory]
    [AutoData]
    public void Choices_keep_their_own_bests(byte difficulty, byte side, ushort streak, uint run, ushort efficiency)
    {
        var plain = ChoiceOf(difficulty, side, false, false, false, false);
        var save = new SaveGame();

        save.RecordInfiniteBest(plain with { Walls = true }, BestOf(streak, run, efficiency));

        Assert.Null(save.InfiniteBestFor(plain));
    }

    public static TheoryData<InfiniteChoice, InfiniteBest> OutOfRange => new()
    {
        { new InfiniteChoice(4, 12, false, false, false, false), new InfiniteBest(-1, 0, null) },
        { new InfiniteChoice(4, 12, false, false, false, false), new InfiniteBest(0, -1, null) },
        { new InfiniteChoice(4, 12, false, false, false, false), new InfiniteBest(0, 0, -1) },
        { new InfiniteChoice(4, 12, false, false, false, false), new InfiniteBest(0, 0, 10001) },
        { new InfiniteChoice(0, 12, false, false, false, false), new InfiniteBest(1, 1, null) },
        { new InfiniteChoice(11, 12, false, false, false, false), new InfiniteBest(1, 1, null) },
        { new InfiniteChoice(4, 1, false, false, false, false), new InfiniteBest(1, 1, null) },
    };

    [Theory]
    [MemberData(nameof(OutOfRange))]
    public void Out_of_range_candidate_throws_before_changing_anything(InfiniteChoice choice, InfiniteBest candidate)
    {
        var save = new SaveGame();

        Assert.Throws<ArgumentOutOfRangeException>(() => save.RecordInfiniteBest(choice, candidate));
        Assert.Null(save.InfiniteBestFor(choice));
    }
}
