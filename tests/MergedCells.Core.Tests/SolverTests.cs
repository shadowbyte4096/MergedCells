using AutoFixture;
using static MergedCells.Core.Tests.TestMaps;

namespace MergedCells.Core.Tests;

public class SolverTests
{
    private sealed record MapCase(
        string Name, Func<Map> Build, int Par, string Path, int ReachableStops, long OptimalRoutes, bool HasTwists = false);

    // One row per hand-made map: name, map, par, path as letters (U, D, L, R), reachable stops, optimal routes
    private static readonly MapCase[] Cases =
    [
        new("Example", Example, 3, "DDR", 16, 1),
        new("Singles3x3", () => Map.Create(AllSingles3x3(), new(0, 0), new(2, 2)), 4, "DDRR", 9, 6),
        // The only 3-move route starts by going up, away from the end
        new("AwayFirst", () => Map.Create(Grid("AAAB", "CDEB", "FGHI"), new(1, 0), new(2, 3)), 3, "URD", 12, 1),
        new("TwoSquares", () => Map.Create(Grid("AB"), new(0, 0), new(0, 1)), 1, "R", 2, 1),
        new("TwoByFive", () => Map.Create(Grid("AAAAB", "CCCCD"), new(0, 0), new(1, 4)), 2, "DR", 6, 2),
        new("OneColumn", () => Map.Create(Grid("A", "A", "B"), new(0, 0), new(2, 0)), 1, "D", 2, 1),
        // [0, 2] can only be reached by sliding on from the end
        new("EndBesideStart", () => Map.Create(Grid("ABC"), new(0, 0), new(0, 1)), 1, "R", 2, 1),
        // Once the player leaves the start they can never stop on it again
        new("StartMidStrip", () => Map.Create(Grid("AAAB"), new(0, 1), new(0, 3)), 1, "R", 3, 1),
        new("RowStrips100", RowStrips100, 100, new string('D', 99) + "R", 200, 100),
        // About 2.3e58 optimal routes, so the count is capped
        new("Singles100", Singles100, 198, new string('D', 99) + new string('R', 99), 10_000, long.MaxValue),
        // Level 26: the wall stops the first slide right above the end
        new("WallStopper", () => Map.Create(Grid("AAAAA", "BCDEF"), new(0, 0), new(1, 2), Walls(2, 5, (0, 3))), 2, "RD", 6, 1, true),
        // Level 27: the walls block the way down the right side. The middle column and [4, 2] can't be reached
        new(
            "WallBlocked",
            () => Map.Create(Grid("AAAAA", "BCCCD", "BEEED", "BFFFD", "GGGGZ"), new(0, 0), new(4, 4), Walls(5, 5, (1, 4), (2, 4))),
            3,
            "DDR",
            18,
            1,
            true),
        new("WallPocket", WallPocket, 1, "D", 4, 1, true),
        // Level 28: the sticky square stops the first slide right beside the end
        new("StickyStopper", () => Map.Create(Grid("AB", "AC", "AZ", "AD", "AE"), new(0, 0), new(2, 1), Sticky(5, 2, (2, 0))), 2, "DR", 10, 1, true),
        // Level 29: the right-hand lane's two sticky squares make it a move slower than going round. Column 2 can't be reached
        new(
            "StickySlowLane",
            () => Map.Create(Grid("AAAAB", "CDDDB", "CEEEB", "CFFFB", "GGGGZ"), new(0, 0), new(4, 4), Sticky(5, 5, (1, 4), (2, 4))),
            3,
            "DDR",
            20,
            1,
            true),
        // Level 30: the top lane's trap catches the quick route along it, so the player goes down first
        new(
            "TrapInTheLane",
            () => Map.Create(Grid("AAAAB", "CCCCB", "DDDDZ"), new(0, 0), new(2, 4), Traps(3, 5, (0, 2))),
            3,
            "DDR",
            9,
            2,
            true),
        // Level 31: the right-hand column's first square is a trap, so the player drops two rows and comes in below it
        new(
            "TrapWhereYoudStop",
            () => Map.Create(Grid("AAAAA", "BBBBC", "DDDDC", "EEEEC", "FFFFZ"), new(0, 0), new(4, 4), Traps(5, 5, (1, 4))),
            4,
            "DDRD",
            14,
            1,
            true),
    ];

    public static TheoryData<string> CaseNames => new(Cases.Select(c => c.Name));

    public static TheoryData<string> PlainCaseNames => new(Cases.Where(c => !c.HasTwists).Select(c => c.Name));

    // The start is mid-strip, so the squares either side of it can never get back above the end
    private static Map WallPocket() => Map.Create(Grid("AAA", "BZC"), new(0, 1), new(1, 1), Walls(2, 3, (1, 0), (1, 2)));

    // Each set starts at a different direction, so any change to the tie-break order fails at least one row
    public static TheoryData<Direction[], Direction> WinningDirections => new()
    {
        { new[] { Direction.Up, Direction.Down, Direction.Left, Direction.Right }, Direction.Up },
        { new[] { Direction.Down, Direction.Left, Direction.Right }, Direction.Down },
        { new[] { Direction.Left, Direction.Right }, Direction.Left },
    };

    private static MapCase Case(string name) => Cases.Single(c => c.Name == name);

    private static Direction[] Directions(string letters) => letters.Select(letter => letter switch
    {
        'U' => Direction.Up,
        'D' => Direction.Down,
        'L' => Direction.Left,
        'R' => Direction.Right,
        _ => throw new ArgumentOutOfRangeException(nameof(letters), letters, "Paths use only U, D, L and R."),
    }).ToArray();

    private static Cell[] Cells(int count) => new Fixture().CreateMany<Cell>(count).ToArray();

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void Par_is_the_fewest_winning_moves(string name)
    {
        var expected = Case(name);

        Assert.Equal(expected.Par, Solver.Solve(expected.Build()).Par);
    }

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void Path_is_the_first_optimal_route_in_direction_order(string name)
    {
        var expected = Case(name);

        Assert.Equal(Directions(expected.Path), Solver.Solve(expected.Build()).Path);
    }

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void Replaying_the_path_wins_in_par_moves(string name)
    {
        var map = Case(name).Build();
        var result = Solver.Solve(map);
        var attempt = new Attempt(map);

        var outcomes = result.Path.Select(attempt.Move).ToList();

        Assert.Equal(Enumerable.Repeat(MoveOutcome.Moved, result.Par!.Value - 1).Append(MoveOutcome.Won), outcomes);
    }

    [Fact]
    public void Relabelled_region_ids_give_the_same_path()
    {
        var ids = new Fixture().CreateMany<int>(8).ToList();
        var relabelled = Map.Create(Relabel(ExampleRegions(), ids), ExampleStart, ExampleEnd);

        Assert.Equal(Solver.Solve(Example()).Path, Solver.Solve(relabelled).Path);
    }

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void Reachable_stops_count_every_square_the_player_can_stand_on(string name)
    {
        var expected = Case(name);

        Assert.Equal(expected.ReachableStops, Solver.Solve(expected.Build()).ReachableStops);
    }

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void Optimal_routes_count_every_shortest_move_sequence(string name)
    {
        var expected = Case(name);

        Assert.Equal(expected.OptimalRoutes, Solver.Solve(expected.Build()).OptimalRoutes);
    }

    // Every plain row in Cases is a rectangle tiling. Irregular regions break this: see the ring tests below
    [Theory]
    [MemberData(nameof(PlainCaseNames))]
    public void Plain_maps_of_rectangles_have_no_dead_ends(string name)
    {
        Assert.Equal(0, Solver.Solve(Case(name).Build()).DeadEnds);
    }

    // The player slides right round the ring and can only stop on its four corners, never beside the end
    [Fact]
    public void Ring_round_the_end_makes_a_plain_map_unsolvable()
    {
        var map = Map.Create(Grid("AAA", "ABA", "AAA"), new(0, 0), new(1, 1));

        var result = Solver.Solve(map);

        Assert.Equal((false, (int?)null, 4, 4), (result.IsSolvable, result.Par, result.ReachableStops, result.DeadEnds));
    }

    // Right wins at once; any other first move strands the player on the ring's corners
    [Fact]
    public void Ring_can_leave_dead_ends_on_a_solvable_plain_map()
    {
        var map = Map.Create(Grid("AAAA", "ABAA", "AAAA"), new(1, 0), new(1, 1));

        var result = Solver.Solve(map);

        Assert.Equal(((int?)1, 6, 4), (result.Par, result.ReachableStops, result.DeadEnds));
    }

    [Fact]
    public void Walls_sealing_the_end_make_the_map_unsolvable()
    {
        var map = Map.Create(Grid("AB", "CD"), new(0, 0), new(1, 1), Walls(2, 2, (0, 1), (1, 0)));

        var result = Solver.Solve(map);

        Assert.Equal((false, (int?)null, 1, 1), (result.IsSolvable, result.Par, result.ReachableStops, result.DeadEnds));
    }

    // [0, 0] and [0, 2]: each can only slide to the other
    [Fact]
    public void Walls_can_leave_dead_ends_on_a_solvable_map()
    {
        var result = Solver.Solve(WallPocket());

        Assert.Equal((true, 2), (result.IsSolvable, result.DeadEnds));
    }

    [Fact]
    public void Relabelled_region_ids_give_the_same_stats()
    {
        var ids = new Fixture().CreateMany<int>(8).ToList();
        var original = Solver.Solve(Example());
        var relabelled = Solver.Solve(Map.Create(Relabel(ExampleRegions(), ids), ExampleStart, ExampleEnd));

        Assert.Equal(
            (original.ReachableStops, original.DeadEnds, original.OptimalRoutes),
            (relabelled.ReachableStops, relabelled.DeadEnds, relabelled.OptimalRoutes));
    }

    [Fact]
    public void Solving_the_same_map_twice_gives_the_same_result()
    {
        var map = Example();

        var first = Solver.Solve(map);
        var second = Solver.Solve(map);

        Assert.Equal(
            (first.Par, first.ReachableStops, first.DeadEnds, first.OptimalRoutes),
            (second.Par, second.ReachableStops, second.DeadEnds, second.OptimalRoutes));
        Assert.Equal(first.Path, second.Path);
    }

    [Fact]
    public void Path_cannot_be_changed_by_the_caller()
    {
        var path = (IList<Direction>)Solver.Solve(Example()).Path;

        Assert.Throws<NotSupportedException>(() => path[0] = Direction.Left);
    }

    [Fact]
    public void Solve_with_a_null_map_throws()
    {
        Assert.Throws<ArgumentNullException>(() => Solver.Solve(null!));
    }

    private static SolveResult SolveCycleWithNoWin()
    {
        var cells = Cells(2);
        var (start, a) = (cells[0], cells[1]);
        var rule = new MoveRule().Slide(start, Direction.Right, a).Slide(a, Direction.Left, start);
        return Solver.Solve(start, rule.Apply);
    }

    [Fact]
    public void Rule_with_no_win_is_not_solvable()
    {
        var result = SolveCycleWithNoWin();

        Assert.Equal((false, (int?)null, 0, 0L), (result.IsSolvable, result.Par, result.Path.Count, result.OptimalRoutes));
    }

    [Fact]
    public void Win_is_decided_by_the_ReachedEnd_flag()
    {
        var start = Cells(1)[0];
        var rule = new MoveRule().Win(start, Direction.Right);

        var result = Solver.Solve(start, rule.Apply);

        Assert.Equal(((int?)1, Direction.Right), (result.Par, result.Path.Single()));
    }

    [Fact]
    public void Every_reachable_square_of_an_unsolvable_rule_is_a_dead_end()
    {
        var result = SolveCycleWithNoWin();

        Assert.Equal((2, 2), (result.ReachableStops, result.DeadEnds));
    }

    [Fact]
    public void Start_with_no_moves_is_a_dead_end()
    {
        var start = Cells(1)[0];

        var result = Solver.Solve(start, new MoveRule().Apply);

        Assert.Equal((1, 1, false), (result.ReachableStops, result.DeadEnds, result.IsSolvable));
    }

    [Fact]
    public void Pocket_that_cannot_win_counts_as_dead_ends()
    {
        var cells = Cells(3);
        var (start, a, b) = (cells[0], cells[1], cells[2]);
        var rule = new MoveRule()
            .Win(start, Direction.Right)
            .Slide(start, Direction.Down, a)
            .Slide(a, Direction.Right, b)
            .Slide(b, Direction.Left, a);

        var result = Solver.Solve(start, rule.Apply);

        Assert.Equal(((int?)1, 4, 2), (result.Par, result.ReachableStops, result.DeadEnds));
    }

    [Fact]
    public void Square_that_wins_only_by_a_detour_is_not_a_dead_end()
    {
        var cells = Cells(3);
        var (start, a, b) = (cells[0], cells[1], cells[2]);
        var rule = new MoveRule()
            .Win(start, Direction.Right)
            .Slide(start, Direction.Down, a)
            .Slide(a, Direction.Down, b)
            .Win(b, Direction.Right);

        var result = Solver.Solve(start, rule.Apply);

        Assert.Equal((4, 0), (result.ReachableStops, result.DeadEnds));
    }

    [Fact]
    public void Slide_that_stops_where_it_started_adds_no_square()
    {
        var start = Cells(1)[0];
        var rule = new MoveRule().Slide(start, Direction.Up, start).Slide(start, Direction.Left, start);

        var result = Solver.Solve(start, rule.Apply);

        Assert.Equal(1, result.ReachableStops);
    }

    [Fact]
    public void Two_winning_directions_from_one_square_are_two_routes()
    {
        var start = Cells(1)[0];
        var rule = new MoveRule().Win(start, Direction.Left).Win(start, Direction.Right);

        var result = Solver.Solve(start, rule.Apply);

        Assert.Equal(((int?)1, 2L), (result.Par, result.OptimalRoutes));
    }

    [Theory]
    [MemberData(nameof(WinningDirections))]
    public void Tied_wins_take_the_first_direction_in_Up_Down_Left_Right_order(Direction[] winning, Direction expected)
    {
        var start = Cells(1)[0];
        var rule = new MoveRule();
        foreach (var direction in winning)
        {
            rule.Win(start, direction);
        }

        var result = Solver.Solve(start, rule.Apply);

        Assert.Equal(expected, result.Path.Single());
    }

    [Fact]
    public void Solve_with_a_null_rule_throws()
    {
        Assert.Throws<ArgumentNullException>(() => Solver.Solve(Cells(1)[0], null!));
    }
}
