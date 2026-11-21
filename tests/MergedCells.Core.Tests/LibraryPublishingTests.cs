using AutoFixture;

namespace MergedCells.Core.Tests;

public class LibraryPublishingTests
{
    private static readonly Fixture Fixture = new();

    private static readonly DateTimeOffset Edited = new(2026, 11, 20, 10, 0, 0, TimeSpan.Zero);

    private static readonly LevelStatus Playable = new(LevelStatusKind.Playable, 3, 10, "Easy");

    private static CustomLevel Level(Map? map = null) => new(Fixture.Create<Guid>(), "Ring Road", map ?? TestMaps.Example(), Edited);

    public static TheoryData<string, PublishState> Cases => new()
    {
        { "not playable", PublishState.NotPlayable },
        { "too big", PublishState.TooBig },
        { "server off", PublishState.Unavailable },
        { "not published", PublishState.Publish },
        { "edited since", PublishState.Update },
        { "up to date", PublishState.UpToDate },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void State_follows_the_level_and_the_save(string scenario, PublishState expected)
    {
        var save = new SaveGame();
        var level = scenario == "too big" ? Level(ShareCodeTests.Worst(255)) : Level();
        if (scenario == "edited since")
        {
            save.RecordPublished(level.Id, Edited.AddDays(-1));
        }
        if (scenario == "up to date")
        {
            save.RecordPublished(level.Id, Edited);
        }
        var status = scenario == "not playable" ? new LevelStatus(LevelStatusKind.NoRoute) : Playable;

        Assert.Equal(expected, LibraryPublishing.StateOf(level, status, save, serverOn: scenario != "server off"));
    }

    [Fact]
    public void Imported_level_has_no_publish_state()
    {
        var level = Level() with { Imported = Edited };

        Assert.Throws<ArgumentException>(() => LibraryPublishing.StateOf(level, Playable, new SaveGame(), serverOn: true));
    }
}
