using AutoFixture;

namespace MergedCells.Core.Tests;

public class LibraryTagsTests
{
    private static readonly Fixture Fixture = new();

    private static readonly DateTimeOffset Edited = new(2026, 11, 20, 10, 0, 0, TimeSpan.Zero);

    private static LibraryEntry Entry(CustomLevel level, bool mine) =>
        new(level.Id, level, new LevelStatus(LevelStatusKind.Playable, 3, 10, "Easy"), 0, 0, mine, false, false);

    private static CustomLevelLoad Load(params CustomLevel[] levels) => new(levels, [], new HashSet<Guid>());

    public static TheoryData<string, LibraryTag, LibraryGet> Cases => new()
    {
        { "mine, not here", LibraryTag.Yours, LibraryGet.Get },
        { "mine, here", LibraryTag.Yours, LibraryGet.Have },
        { "not here", LibraryTag.None, LibraryGet.Get },
        { "imported, same", LibraryTag.InYourLevels, LibraryGet.Have },
        { "imported, older", LibraryTag.Update, LibraryGet.Newer },
        { "imported, newer", LibraryTag.InYourLevels, LibraryGet.Have },
        { "own, not mine", LibraryTag.InYourLevels, LibraryGet.Have },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Tag_and_get_follow_what_the_phone_holds(string scenario, LibraryTag tag, LibraryGet get)
    {
        var level = new CustomLevel(Fixture.Create<Guid>(), "Ring Road", TestMaps.Example(), Edited);
        var (mine, load) = scenario switch
        {
            "mine, not here" => (true, Load()),
            "mine, here" => (true, Load(level)),
            "not here" => (false, Load()),
            "imported, same" => (false, Load(level with { Imported = Edited })),
            "imported, older" => (false, Load(level with { Edited = Edited.AddDays(-1), Imported = Edited })),
            "imported, newer" => (false, Load(level with { Edited = Edited.AddDays(1), Imported = Edited })),
            _ => (false, Load(level)),
        };

        Assert.Equal((tag, get), LibraryTags.For(Entry(level, mine), load));
    }
}
