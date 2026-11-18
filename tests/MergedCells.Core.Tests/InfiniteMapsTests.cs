using AutoFixture;
using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

public class InfiniteMapsTests
{
    private static readonly InfiniteConfig Config = InfiniteConfigs.With();

    private static InfiniteMaps Maps(InfiniteConfig? config = null) =>
        new(config ?? Config, GeneratorConfigs.With(), DifficultyConfigs.With(), ClimbConfigs.With());

    private static InfiniteChoice Plain(int difficulty, int side) => new(difficulty, side, false, false, false, false);

    private static IEnumerable<Tile> Tiles(Map map)
    {
        for (var row = 0; row < map.Rows; row++)
        {
            for (var column = 0; column < map.Columns; column++)
            {
                yield return map.TileAt(new Cell(row, column));
            }
        }
    }

    [Theory]
    [AutoData]
    public void Request_carries_the_choice(bool walls, bool traps, bool teleporters, bool irregular)
    {
        var choice = new InfiniteChoice(7, 12, walls, traps, teleporters, irregular);

        var request = Maps().Request(choice);

        Assert.Equal(
            new ClimbRequest(new MapRequest(12, Config.MinPar, Config.MaxPar, irregular, choice.Twists(Config)), new DifficultyTarget(60, 69), DifficultyMode.Infinite),
            request);
    }

    [Theory]
    [AutoData]
    public void In_band_says_whether_the_score_is_in_the_band(ulong seed)
    {
        var map = Maps().Next(Plain(4, 12), seed)!;

        Assert.Equal(map.Score is >= 30 and <= 39, map.InBand);
    }

    // Whether a climb hits depends on its seed; these two hit
    [Theory]
    [InlineData(1ul)]
    [InlineData(2ul)]
    public void Reachable_band_is_hit(ulong seed)
    {
        var map = Maps().Next(Plain(3, 12), seed)!;

        Assert.True(map.InBand && map.Score is >= 20 and <= 29);
    }

    // A plain 4×4 map has no room for the tricks a score of 90 needs
    [Theory]
    [AutoData]
    public void Out_of_reach_band_keeps_the_nearest_map(ulong seed)
    {
        var map = Maps().Next(Plain(10, 4), seed)!;

        Assert.False(map.InBand);
    }

    [Theory]
    [AutoData]
    public void Kept_map_has_the_solvers_par(ulong seed)
    {
        var map = Maps().Next(Plain(10, 4), seed)!;

        Assert.Equal(Solver.Solve(map.Map).Par, map.Par);
    }

    [Theory]
    [AutoData]
    public void Kept_map_has_par_in_range(ulong seed)
    {
        var map = Maps().Next(Plain(5, 8), seed)!;

        Assert.InRange(map.Par, Config.MinPar, Config.MaxPar);
    }

    [Theory]
    [AutoData]
    public void Same_seed_and_choice_give_the_same_map(ulong seed)
    {
        var first = Maps().Next(Plain(5, 8), seed)!;
        var second = Maps().Next(Plain(5, 8), seed)!;

        Assert.Equal(LevelFingerprint.Of(first.Map, first.Par), LevelFingerprint.Of(second.Map, second.Par));
    }

    [Theory]
    [AutoData]
    public void Plain_choice_has_no_twists(ulong seed)
    {
        var map = Maps().Next(Plain(5, 8), seed)!.Map;

        Assert.Equal((false, 0), (map.HasTiles, map.Teleporters.Count));
    }

    public static TheoryData<bool, bool, bool> OneTwist => new()
    {
        { true, false, false },
        { false, true, false },
        { false, false, true },
    };

    [Theory]
    [MemberData(nameof(OneTwist))]
    public void Switched_on_twist_is_placed_and_others_are_not(bool walls, bool traps, bool teleporters)
    {
        var map = Maps().Next(new InfiniteChoice(5, 8, walls, traps, teleporters, false), new Fixture().Create<ulong>())!.Map;
        var tiles = Tiles(map).ToList();

        Assert.Equal(
            (walls, traps, teleporters),
            (tiles.Contains(Tile.Wall), tiles.Contains(Tile.Trap), map.Teleporters.Count > 0));
    }

    [Theory]
    [AutoData]
    public void Unreachable_par_range_gives_null(ulong seed)
    {
        Assert.Null(Maps(InfiniteConfigs.With(minPar: 500, maxPar: 500)).Next(Plain(3, 4), seed));
    }
}
