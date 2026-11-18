using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

public class InfiniteChoiceTests
{
    // Difficulty 10 allows plain maps from 8 and twist maps from 4; difficulty 4 allows plain maps up to 16
    private static readonly InfiniteConfig Config = InfiniteConfigs.With(difficulties: Difficulties());

    private static InfiniteDifficulty[] Difficulties()
    {
        var all = InfiniteConfigs.Tenths().ToArray();
        all[3] = InfiniteConfigs.Difficulty(30, 39, InfiniteConfigs.Range(4, 16), InfiniteConfigs.Range(4, 30));
        all[9] = InfiniteConfigs.Difficulty(90, 100, InfiniteConfigs.Range(8, 30), InfiniteConfigs.Range(4, 30));
        return all;
    }

    private static InfiniteChoice Plain(int difficulty, int side) => new(difficulty, side, false, false, false, false);

    [Fact]
    public void Start_is_the_configs_start_with_every_switch_off()
    {
        Assert.Equal(new InfiniteChoice(3, 10, false, false, false, false), InfiniteChoice.Start(InfiniteConfigs.With(startDifficulty: 3, startSide: 10)));
    }

    [Theory]
    [InlineData(5, 10, 8)]
    [InlineData(25, 4, 16)]
    [InlineData(12, 4, 12)]
    public void Changing_difficulty_pulls_the_size_into_its_range(int side, int difficulty, int expected)
    {
        Assert.Equal(expected, Plain(1, side).WithDifficulty(difficulty, Config).Side);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void Difficulty_outside_one_to_ten_throws(int difficulty)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Plain(1, 10).WithDifficulty(difficulty, Config));
    }

    [Fact]
    public void First_twist_on_switches_to_the_twists_range()
    {
        var choice = Plain(10, 8).WithWalls(true, Config).WithSide(5, Config);

        Assert.Equal(5, choice.Side);
    }

    [Fact]
    public void Last_twist_off_switches_back_to_the_plain_range()
    {
        var choice = Plain(10, 8).WithTraps(true, Config).WithSide(5, Config).WithTraps(false, Config);

        Assert.Equal(8, choice.Side);
    }

    [Fact]
    public void Teleporters_choose_the_twists_range()
    {
        Assert.Equal(InfiniteConfigs.Range(4, 30), Plain(10, 8).WithTeleporters(true, Config).Sizes(Config));
    }

    [Fact]
    public void Irregular_alone_keeps_the_plain_range()
    {
        Assert.Equal(InfiniteConfigs.Range(8, 30), Plain(10, 8).WithIrregular(true).Sizes(Config));
    }

    [Theory]
    [InlineData(3, 8)]
    [InlineData(31, 30)]
    [InlineData(17, 17)]
    public void Side_is_clamped(int side, int expected)
    {
        Assert.Equal(expected, Plain(10, 8).WithSide(side, Config).Side);
    }

    [Theory]
    [InlineData(1, 100, 1, 30)]
    [InlineData(0, 10, 3, 10)]
    [InlineData(11, 10, 3, 10)]
    [InlineData(10, 4, 10, 8)]
    public void Fitted_clamps_a_saved_choice_into_this_config(int difficulty, int side, int expectedDifficulty, int expectedSide)
    {
        var fitted = Plain(difficulty, side).Fitted(Config);

        Assert.Equal((expectedDifficulty, expectedSide), (fitted.Difficulty, fitted.Side));
    }

    [Theory]
    [AutoData]
    public void Twists_are_null_without_walls_traps_or_teleporters(bool irregular)
    {
        Assert.Null(new InfiniteChoice(3, 10, false, false, false, irregular).Twists(Config));
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void Twists_hold_each_switched_on_density(bool walls, bool traps, bool teleporters)
    {
        var config = InfiniteConfigs.With(wallPercent: 7, trapPercent: 5, teleporterPercent: 3);

        var twists = new InfiniteChoice(3, 10, walls, traps, teleporters, false).Twists(config);

        Assert.Equal(new TwistDensities(walls ? 7 : 0, 0, traps ? 5 : 0, teleporters ? 3 : 0), twists);
    }
}
