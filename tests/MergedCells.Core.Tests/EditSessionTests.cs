using AutoFixture;
using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

public class EditSessionTests
{
    private readonly Fixture fixture = new();

    private DateTimeOffset clock = new(2026, 11, 17, 10, 0, 0, TimeSpan.Zero);

    private EditSession Session() =>
        new(new CustomLevel(fixture.Create<Guid>(), fixture.Create<string>(), LevelEdits.Blank(3, 4), clock), () => clock);

    // Imported levels are edited only as a copy
    [Fact]
    public void Imported_level_can_t_be_edited()
    {
        var level = new CustomLevel(fixture.Create<Guid>(), fixture.Create<string>(), LevelEdits.Blank(3, 4), clock, clock);

        Assert.Throws<ArgumentException>(() => new EditSession(level, () => clock));
    }

    [Fact]
    public void Applied_change_gives_the_level_to_write_with_the_clock_time()
    {
        var session = Session();
        clock = clock.AddMinutes(5);

        var level = session.Apply(LevelEdits.Paint(session.Level.Map, [new(0, 0), new(0, 1)]));

        Assert.NotNull(level);
        Assert.Equal(clock, level.Edited);
        Assert.Same(level, session.Level);
    }

    public static TheoryData<bool> NoChange => new() { true, false };

    [Theory]
    [MemberData(nameof(NoChange))]
    public void Unchanged_or_refused_results_give_nothing(bool refused)
    {
        var session = Session();
        var map = session.Level.Map;

        var level = session.Apply(refused ? EditResult.Refused(map, "No") : EditResult.Unchanged(map));

        Assert.Null(level);
        Assert.False(session.CanUndo);
    }

    [Theory]
    [AutoData]
    public void Rename_gives_the_level_but_is_not_an_undo_step(string name)
    {
        var session = Session();

        var level = session.Rename(name);

        Assert.Equal(name, level?.Name);
        Assert.False(session.CanUndo);
    }

    [Fact]
    public void Rename_to_the_same_name_gives_nothing()
    {
        var session = Session();

        Assert.Null(session.Rename(session.Level.Name));
    }

    [Fact]
    public void Undo_restores_the_size_before_a_resize()
    {
        var session = Session();
        var before = session.Level.Map;
        session.Apply(LevelEdits.Resize(before, 5, 6));

        var level = session.Undo();

        Assert.Same(before, level?.Map);
    }

    [Fact]
    public void Redo_puts_the_edit_back()
    {
        var session = Session();
        var resized = LevelEdits.Resize(session.Level.Map, 5, 6);
        session.Apply(resized);
        session.Undo();

        Assert.Same(resized.Map, session.Redo()?.Map);
    }

    [Fact]
    public void Edited_is_whole_seconds()
    {
        var session = Session();
        clock = clock.AddMilliseconds(1500);

        var level = session.Rename(fixture.Create<string>());

        Assert.Equal(clock.AddMilliseconds(-500), level?.Edited);
    }
}
