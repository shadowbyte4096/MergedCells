using AutoFixture;
using static MergedCells.Core.Tests.TestMaps;

namespace MergedCells.Core.Tests;

// Made-up rules put the end at [4, 4], so each square's coordinates set how far it is from the end
public class DifficultyRaterTests
{
    private static readonly Cell End = new(4, 4);

    private static DifficultyMetrics? Measure(MoveRule rule, Cell start, int nearOptimalExtraMoves = 2) =>
        new DifficultyRater(DifficultyConfigs.With(nearOptimalExtraMoves: nearOptimalExtraMoves)).Measure(start, End, rule.Apply);

    private static DifficultyMetrics? Measure(Map map) => new DifficultyRater(DifficultyConfigs.With()).Measure(map);

    // Down to [4, 0], then Right wins
    private static readonly MoveRule Straight = new MoveRule()
        .Slide(new(0, 0), Direction.Down, new(4, 0))
        .Win(new(4, 0), Direction.Right);

    // Up to [0, 2], two squares further from the end, then Right wins
    private static readonly MoveRule BacktrackOnly = new MoveRule()
        .Slide(new(2, 2), Direction.Up, new(0, 2))
        .Win(new(0, 2), Direction.Right);

    // Up then Right, or Down then Right: both win in two
    private static readonly MoveRule TwoOptimal = new MoveRule()
        .Slide(new(2, 2), Direction.Up, new(0, 2))
        .Win(new(0, 2), Direction.Right)
        .Slide(new(2, 2), Direction.Down, new(3, 2))
        .Win(new(3, 2), Direction.Right);

    // Up then Right wins in two; Down, Down, Right never moves away but takes three
    private static readonly MoveRule GreedyLonger = new MoveRule()
        .Slide(new(2, 2), Direction.Up, new(0, 2))
        .Win(new(0, 2), Direction.Right)
        .Slide(new(2, 2), Direction.Down, new(3, 2))
        .Slide(new(3, 2), Direction.Down, new(4, 2))
        .Win(new(4, 2), Direction.Right);

    public static TheoryData<string> ParMaps => new("Example", "Sketch", "Singles3x3", "TrappedLane", "RowStrips100");

    private static Map ParMap(string name) => name switch
    {
        "Example" => Example(),
        "Sketch" => Sketch(),
        "Singles3x3" => Map.Create(AllSingles3x3(), new(0, 0), new(2, 2)),
        "TrappedLane" => TrappedLane(),
        "RowStrips100" => RowStrips100(),
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    [Fact]
    public void Constructor_names_the_value_out_of_range()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new DifficultyRater(DifficultyConfigs.With(caps: DifficultyConfigs.Caps(decoys: 0))));

        Assert.Equal("difficulty.caps.decoys", exception.ParamName);
    }

    [Fact]
    public void Constructor_names_the_override_whose_weights_add_up_to_0()
    {
        var modeWeights = new Dictionary<string, DifficultyWeights> { ["daily"] = DifficultyConfigs.Weights(0, 0, 0, 0, 0) };

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new DifficultyRater(DifficultyConfigs.With(modeWeights: modeWeights)));

        Assert.Equal("difficulty.modeWeights.daily", exception.ParamName);
    }

    [Fact]
    public void Constructor_names_an_override_weight_out_of_range()
    {
        var modeWeights = new Dictionary<string, DifficultyWeights> { ["daily"] = DifficultyConfigs.Weights(decoys: 10_001) };

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new DifficultyRater(DifficultyConfigs.With(modeWeights: modeWeights)));

        Assert.Equal("difficulty.modeWeights.daily.decoys", exception.ParamName);
    }

    // Parse rejects such a key in the file; a config built in code must not have it silently ignored
    [Fact]
    public void Constructor_rejects_a_key_that_isn_t_a_mode()
    {
        var modeWeights = new Dictionary<string, DifficultyWeights> { ["versus"] = DifficultyConfigs.Weights() };

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new DifficultyRater(DifficultyConfigs.With(modeWeights: modeWeights)));

        Assert.Equal("difficulty.modeWeights", exception.ParamName);
    }

    public static TheoryData<DifficultyConfig, string> MissingParts => new()
    {
        { DifficultyConfigs.With() with { Caps = null! }, "difficulty.caps" },
        { DifficultyConfigs.With() with { Weights = null! }, "difficulty.weights" },
        { DifficultyConfigs.With() with { ModeWeights = null! }, "difficulty.modeWeights" },
        { DifficultyConfigs.With(modeWeights: new Dictionary<string, DifficultyWeights> { ["daily"] = null! }), "difficulty.modeWeights.daily" },
    };

    // Parse null-checks the file; a config built in code gets the same names
    [Theory]
    [MemberData(nameof(MissingParts))]
    public void Constructor_names_a_missing_part(DifficultyConfig config, string name)
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new DifficultyRater(config));

        Assert.Equal(name, exception.ParamName);
    }

    [Theory]
    [MemberData(nameof(ParMaps))]
    public void Par_is_the_solver_s_par(string name)
    {
        var map = ParMap(name);

        Assert.Equal(Solver.Solve(map).Par, Measure(map)!.Par);
    }

    [Fact]
    public void A_route_straight_to_the_end_has_no_tricks()
    {
        Assert.Equal(new DifficultyMetrics(2, 0, 0, 0, 0, 0, 0), Measure(Straight, new(0, 0)));
    }

    [Fact]
    public void Away_moves_and_retreat_count_the_only_route_s_backtracking()
    {
        var metrics = Measure(BacktrackOnly, new(2, 2))!;

        Assert.Equal((1, 2), (metrics.AwayMoves, metrics.Retreat));
    }

    [Fact]
    public void The_easiest_optimal_route_s_away_moves_count()
    {
        Assert.Equal(0, Measure(TwoOptimal, new(2, 2))!.AwayMoves);
    }

    // Up to [1, 3] keeps the distance at 4, as a slide past the end's row or column can, then Right wins
    private static readonly MoveRule Sideways = new MoveRule()
        .Slide(new(2, 2), Direction.Up, new(1, 3))
        .Win(new(1, 3), Direction.Right);

    [Fact]
    public void A_move_that_keeps_its_distance_is_no_away_move()
    {
        Assert.Equal(0, Measure(Sideways, new(2, 2))!.AwayMoves);
    }

    [Fact]
    public void A_greedy_route_may_keep_its_distance()
    {
        Assert.Equal(0, Measure(Sideways, new(2, 2))!.GreedyGap);
    }

    [Fact]
    public void Greedy_gap_is_the_greedy_route_s_extra_moves()
    {
        Assert.Equal(1, Measure(GreedyLonger, new(2, 2))!.GreedyGap);
    }

    [Fact]
    public void Greedy_gap_is_null_without_a_greedy_route()
    {
        Assert.Null(Measure(BacktrackOnly, new(2, 2))!.GreedyGap);
    }

    // Down lands 3 from the end, nearer than the route's Up, which lands 6 away, but takes a move longer
    [Fact]
    public void A_worse_move_that_looks_as_good_is_a_decoy()
    {
        Assert.Equal(1, Measure(GreedyLonger, new(2, 2))!.Decoys);
    }

    // Up lands 6 from the end, further than the route's Down at 3
    [Fact]
    public void A_worse_move_that_lands_further_is_no_decoy()
    {
        var rule = new MoveRule()
            .Slide(new(2, 2), Direction.Down, new(3, 2))
            .Win(new(3, 2), Direction.Right)
            .Slide(new(2, 2), Direction.Up, new(0, 2))
            .Slide(new(0, 2), Direction.Right, new(0, 4))
            .Win(new(0, 4), Direction.Down);

        Assert.Equal(0, Measure(rule, new(2, 2))!.Decoys);
    }

    // Right lands on [2, 3], 3 from the end like the route's Down, in a pocket that never reaches the end
    [Fact]
    public void A_move_into_a_dead_end_can_be_a_decoy()
    {
        var rule = new MoveRule()
            .Slide(new(2, 2), Direction.Down, new(3, 2))
            .Win(new(3, 2), Direction.Right)
            .Slide(new(2, 2), Direction.Right, new(2, 3))
            .Slide(new(2, 3), Direction.Right, new(2, 4))
            .Slide(new(2, 4), Direction.Left, new(2, 3));

        Assert.Equal(1, Measure(rule, new(2, 2))!.Decoys);
    }

    // Both routes take three moves through [3, 3]. Through [3, 0], which comes first in direction order, Down to [4, 2]
    // looks as good as Right but leads back to the start; through [0, 3] there's nothing to fall for
    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    public void The_easiest_optimal_route_s_decoys_count(bool withTopRoute, int expected)
    {
        var rule = new MoveRule()
            .Slide(new(0, 0), Direction.Down, new(3, 0))
            .Slide(new(3, 0), Direction.Right, new(3, 3))
            .Slide(new(3, 0), Direction.Down, new(4, 2))
            .Slide(new(4, 2), Direction.Left, new(0, 0))
            .Win(new(3, 3), Direction.Down);
        if (withTopRoute)
        {
            rule.Slide(new(0, 0), Direction.Right, new(0, 3)).Slide(new(0, 3), Direction.Down, new(3, 3));
        }

        Assert.Equal(expected, Measure(rule, new(0, 0))!.Decoys);
    }

    // Par is s, a, win. One move over: s, b, c, win. Two over: s, a, s, a, win
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    public void Near_optimal_routes_count_by_length(int extraMoves, long expected)
    {
        var cells = new Fixture().CreateMany<Cell>(4).ToArray();
        var (s, a, b, c) = (cells[0], cells[1], cells[2], cells[3]);
        var rule = new MoveRule()
            .Slide(s, Direction.Right, a)
            .Win(a, Direction.Down)
            .Slide(a, Direction.Left, s)
            .Slide(s, Direction.Down, b)
            .Slide(b, Direction.Right, c)
            .Win(c, Direction.Down);

        Assert.Equal(expected, Measure(rule, s, extraMoves)!.NearOptimalRoutes);
    }

    // About 10^60 routes win in par + 1 or par + 2 moves
    [Fact]
    public void Near_optimal_routes_are_capped()
    {
        Assert.Equal(long.MaxValue, Measure(Singles100())!.NearOptimalRoutes);
    }

    [Fact]
    public void A_map_that_can_t_be_won_has_no_metrics()
    {
        var cells = new Fixture().CreateMany<Cell>(2).ToArray();
        var rule = new MoveRule().Slide(cells[0], Direction.Right, cells[1]).Slide(cells[1], Direction.Left, cells[0]);

        Assert.Null(Measure(rule, cells[0]));
    }

    // Walls on both sides of the start: it has no moves at all
    [Fact]
    public void A_start_with_no_moves_has_no_metrics()
    {
        var map = Map.Create(Grid("AB", "CD"), new(0, 0), new(1, 1), Walls(2, 2, (0, 1), (1, 0)));

        Assert.Null(Measure(map));
    }

    [Fact]
    public void Measure_with_a_null_map_throws()
    {
        Assert.Throws<ArgumentNullException>(() => new DifficultyRater(DifficultyConfigs.With()).Measure(null!));
    }
}
