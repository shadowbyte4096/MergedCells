using static MergedCells.Core.Tests.TestMaps;

namespace MergedCells.Core.Tests;

// Every test runs the slide on another thread under a timeout, so a broken loop guard fails the test instead of hanging the run
public class TeleporterLoopTests
{
    private const int Timeout = 10_000;

    private static Task<SlideResult> SlideAsync(Map map, Cell from, Direction direction, CancellationToken token) =>
        Task.Run(() => map.Slide(from, direction), token);

    private static readonly Direction[] Directions = [Direction.Up, Direction.Down, Direction.Left, Direction.Right];

    [Theory(Timeout = Timeout)]
    [InlineData(Direction.Left)]
    [InlineData(Direction.Right)]
    public async Task Looping_slide_is_a_no_op_flagged_Looped(Direction direction)
    {
        Assert.Equal(new SlideResult(new(0, 2), false, Looped: true), await SlideAsync(LoopingLane(), new(0, 2), direction, TestContext.Current.CancellationToken));
    }

    [Fact(Timeout = Timeout)]
    public async Task Sliding_from_one_neighbouring_partner_into_the_other_loops()
    {
        var map = Map.Create(Grid("AAAAA", "BBBBZ"), new(0, 0), new(1, 4), teleporters: [Pair(0, 2, 0, 3)]);

        Assert.Equal(new SlideResult(new(0, 2), false, Looped: true), await SlideAsync(map, new(0, 2), Direction.Right, TestContext.Current.CancellationToken));
    }

    [Fact(Timeout = Timeout)]
    public async Task Loop_through_two_pairs_is_flagged()
    {
        var map = Map.Create(Grid("AAAAA", "BBBBB", "CCCCZ"), new(0, 0), new(2, 4), teleporters: [Pair(0, 3, 1, 0), Pair(1, 2, 0, 1)]);

        Assert.Equal(new SlideResult(new(0, 2), false, Looped: true), await SlideAsync(map, new(0, 2), Direction.Right, TestContext.Current.CancellationToken));
    }

    // Out of [0, 1], the slide runs back over [0, 2], where it began
    [Fact(Timeout = Timeout)]
    public async Task Slide_that_passes_back_over_its_start_loops()
    {
        var map = Map.Create(Grid("AAAAA", "BBBBZ"), new(0, 0), new(1, 4), teleporters: [Pair(0, 1, 0, 4)]);

        Assert.Equal(new SlideResult(new(0, 2), false, Looped: true), await SlideAsync(map, new(0, 2), Direction.Right, TestContext.Current.CancellationToken));
    }

    // Into [1, 1], out of [0, 3]; into [0, 5], out of [0, 1]; into [0, 3], out of [1, 1]; then along row 1 to the edge.
    // Three jumps on a map with four teleporter squares, one pair entered from both ends
    [Fact(Timeout = Timeout)]
    public async Task Slide_through_both_squares_of_a_pair_that_stops_is_not_a_loop()
    {
        var map = Map.Create(Grid("AAAAAA", "BBBBBB", "CCCCCZ"), new(0, 0), new(2, 5), teleporters: [Pair(0, 3, 1, 1), Pair(0, 5, 0, 1)]);

        Assert.Equal(new SlideResult(new(1, 5), false, Teleported: true), await SlideAsync(map, new(1, 0), Direction.Right, TestContext.Current.CancellationToken));
    }

    // Into [0, 1], out of [0, 3]; into [0, 4], out of [0, 2]; into [0, 3], out of [0, 1]; into [0, 2], out of [0, 4]; then on
    // to the edge. Four jumps on four teleporter squares, the most a slide that stops can make, so it pins the guard's bound
    [Fact(Timeout = Timeout)]
    public async Task Slide_that_enters_every_teleporter_square_once_is_not_a_loop()
    {
        var map = Map.Create(Grid("AAAAAAA", "BBBBBBZ"), new(0, 0), new(1, 6), teleporters: [Pair(0, 1, 0, 3), Pair(0, 4, 0, 2)]);

        Assert.Equal(new SlideResult(new(0, 6), false, Teleported: true), await SlideAsync(map, new(0, 0), Direction.Right, TestContext.Current.CancellationToken));
    }

    public static TheoryData<string[]> Layouts => new()
    {
        new[] { "AAA", "AAA", "AAZ" },
        new[] { "AAA", "BBB", "CCZ" },
        new[] { "ABC", "ABC", "ABZ" },
        new[] { "ABC", "DEF", "GHZ" },
    };

    [Theory(Timeout = Timeout)]
    [MemberData(nameof(Layouts))]
    public async Task Guard_agrees_with_a_walk_that_remembers_its_squares(string[] layout)
    {
        var mismatches = await Task.Run(() => Mismatches(layout), TestContext.Current.CancellationToken);

        Assert.Empty(mismatches);
    }

    // Every placement of one or two pairs on the squares off the start and end, and every slide on each
    private static List<string> Mismatches(string[] layout)
    {
        var regions = Grid(layout);
        var start = new Cell(0, 0);
        var end = new Cell(2, 2);
        var free = Enumerable.Range(0, 9).Select(i => new Cell(i / 3, i % 3)).Where(c => c != start && c != end).ToArray();
        var mismatches = new List<string>();
        foreach (var pairs in Placements(free))
        {
            var map = Map.Create(regions, start, end, teleporters: pairs);
            for (var row = 0; row < 3; row++)
            {
                for (var column = 0; column < 3; column++)
                {
                    foreach (var direction in Directions)
                    {
                        var from = new Cell(row, column);
                        var expected = Reference(map, from, direction);
                        var actual = map.Slide(from, direction);
                        if (actual != expected)
                        {
                            mismatches.Add($"{string.Join(" ", pairs)} from {from} {direction}: {actual}, expected {expected}");
                        }
                    }
                }
            }
        }
        return mismatches;
    }

    private static IEnumerable<TeleporterPair[]> Placements(Cell[] free)
    {
        for (var a = 0; a < free.Length; a++)
        {
            for (var b = a + 1; b < free.Length; b++)
            {
                var first = new TeleporterPair(free[a], free[b]);
                yield return [first];
                var rest = free.Where((_, i) => i != a && i != b).ToArray();
                for (var c = 0; c < rest.Length; c++)
                {
                    for (var d = c + 1; d < rest.Length; d++)
                    {
                        // Each set of two pairs once: the second pair's first square comes after the first pair's
                        if (Index(free, rest[c]) > a)
                        {
                            yield return [first, new TeleporterPair(rest[c], rest[d])];
                        }
                    }
                }
            }
        }
    }

    private static int Index(Cell[] cells, Cell cell) => Array.IndexOf(cells, cell);

    // The slide rule for a map with no tiles, finding a loop by remembering every square it stands on: a slide keeps
    // one direction, so standing on a square twice repeats everything after it
    private static SlideResult Reference(Map map, Cell from, Direction direction)
    {
        var partners = new Dictionary<Cell, Cell>();
        foreach (var pair in map.Teleporters)
        {
            partners[pair.First] = pair.Second;
            partners[pair.Second] = pair.First;
        }
        var stoodOn = new HashSet<Cell> { from };
        var current = from;
        var teleported = false;
        while (true)
        {
            var next = current.Step(direction);
            if (!map.Contains(next))
            {
                return new SlideResult(current, false, Teleported: teleported);
            }
            if (next == map.End)
            {
                return new SlideResult(next, true, Teleported: teleported);
            }
            if (partners.TryGetValue(next, out var partner))
            {
                current = partner;
                teleported = true;
            }
            else if (map.RegionAt(next) != map.RegionAt(current))
            {
                return new SlideResult(next, false, Teleported: teleported);
            }
            else
            {
                current = next;
            }
            if (!stoodOn.Add(current))
            {
                return new SlideResult(from, false, Looped: true);
            }
        }
    }
}
