using System.Globalization;
using System.Text.RegularExpressions;
using AutoFixture.Xunit3;
using MergedCells.Curation;

namespace MergedCells.Core.Tests;

// On the shipped config, so the real file's layout is what's edited. The IDs come from the file, so promoting and
// retiring levels in 21b never breaks these
public class LevelStarsEditTests
{
    private static string Shipped() => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "game_config.json"));

    private static IReadOnlyCollection<int> ShippedIds() => GameConfig.Parse(Shipped()).Campaign.LevelStars.Keys.ToArray();

    // An ID with an entry (chapter 1's medium and hard levels have them, pinned by StarLimitsTests), and one without
    private static int WithEntry() => ShippedIds().Min();

    private static int WithoutEntry() => ShippedIds().Max() + 1;

    // The shipped file with no levelStars entries
    private static string Emptied() =>
        Regex.Replace(Shipped(), "\"levelStars\": \\{\n.*?\n    \\},", "\"levelStars\": {},", RegexOptions.Singleline);

    private static readonly StarsConfig Limits = new()
    {
        TwoStarMinExtraMoves = 2,
        TwoStarExtraParPercent = 0,
        OneStarMinExtraMoves = 4,
        OneStarExtraParPercent = 0,
    };

    [Fact]
    public void Add_then_remove_gives_back_the_shipped_config()
    {
        Assert.Equal(Shipped(), LevelStarsEdit.Remove(LevelStarsEdit.Add(Shipped(), WithoutEntry(), Limits), WithoutEntry()));
    }

    [Theory]
    [AutoData]
    public void Added_entry_parses_to_the_given_limits(int twoStar, int twoStarPercent, int oneStar, int oneStarPercent)
    {
        var limits = new StarsConfig
        {
            TwoStarMinExtraMoves = twoStar,
            TwoStarExtraParPercent = twoStarPercent,
            OneStarMinExtraMoves = oneStar,
            OneStarExtraParPercent = oneStarPercent,
        };

        Assert.Equal(limits, GameConfig.Parse(LevelStarsEdit.Add(Shipped(), WithoutEntry(), limits)).Campaign.LevelStars[WithoutEntry()]);
    }

    [Fact]
    public void Remove_drops_only_that_entry()
    {
        var after = GameConfig.Parse(LevelStarsEdit.Remove(Shipped(), WithEntry())).Campaign.LevelStars.Keys;

        Assert.Equal(ShippedIds().Where(id => id != WithEntry()).Order(), after.Order());
    }

    [Fact]
    public void Remove_of_an_id_with_no_entry_changes_nothing()
    {
        Assert.Equal(Shipped(), LevelStarsEdit.Remove(Shipped(), WithoutEntry()));
    }

    [Fact]
    public void Add_to_an_empty_object_then_remove_gives_it_back()
    {
        Assert.Equal(Emptied(), LevelStarsEdit.Remove(LevelStarsEdit.Add(Emptied(), 34, Limits), 34));
    }

    [Fact]
    public void Add_to_an_empty_object_parses()
    {
        Assert.Equal(Limits, GameConfig.Parse(LevelStarsEdit.Add(Emptied(), 34, Limits)).Campaign.LevelStars[34]);
    }

    [Fact]
    public void Add_refuses_an_id_already_there()
    {
        Assert.Throws<InvalidOperationException>(() => LevelStarsEdit.Add(Shipped(), WithEntry(), Limits));
    }

    [Fact]
    public void Add_refuses_an_entry_over_two_lines()
    {
        var key = $"\"{WithEntry().ToString(CultureInfo.InvariantCulture)}\": {{ ";
        var split = Shipped().Replace(key, key.TrimEnd() + "\n        ");

        Assert.Throws<InvalidOperationException>(() => LevelStarsEdit.Add(split, WithoutEntry(), Limits));
    }
}
