using static MergedCells.Core.Tests.TestMaps;

namespace MergedCells.Core.Tests;

public class RectangleColoringTests
{
    private static readonly Dictionary<string, Func<Map>> Maps = new()
    {
        ["Example"] = Example,
        ["Sketch"] = Sketch,
        ["Singles3x3"] = () => Map.Create(AllSingles3x3(), new(0, 0), new(2, 2)),
        ["OddWheel"] = OddWheel,
        ["RowStrips100"] = RowStrips100,
        ["Singles100"] = Singles100,
        // A is a ring round C, and the L-shaped B shares an edge with it
        ["Irregular"] = () => Map.Create(Grid("AAABB", "ACADB", "AAADE", "FFFFZ"), new(0, 0), new(3, 4)),
    };

    public static TheoryData<string, int> MapsAndColorCounts
    {
        get
        {
            var data = new TheoryData<string, int>();
            foreach (var name in Maps.Keys)
            {
                data.Add(name, 5);
                data.Add(name, 6);
            }
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(MapsAndColorCounts))]
    public void Neighbours_never_share_a_color(string name, int colors)
    {
        var map = Maps[name]();

        AssertProper(map, RectangleColoring.Assign(map, colors), colors);
    }

    [Fact]
    public void Every_4x4_tiling_is_colored_properly()
    {
        foreach (var regions in Tilings(4, 4))
        {
            if (FirstSingleAfterOrigin(regions) is not { } end)
            {
                continue;
            }
            var map = Map.Create(regions, new(0, 0), end);

            AssertProper(map, RectangleColoring.Assign(map, RectangleColoring.MinimumColors), RectangleColoring.MinimumColors);
        }
    }

    // Checks the enumerator the 4x4 test relies on
    [Fact]
    public void Enumerator_finds_every_4x4_tiling()
    {
        Assert.Equal(70_878, Tilings(4, 4).Count());
    }

    [Fact]
    public void Same_map_same_colors()
    {
        Assert.Equal(RectangleColoring.Assign(Sketch(), 5), RectangleColoring.Assign(Sketch(), 5));
    }

    [Fact]
    public void Five_colors_are_all_used_on_single_squares()
    {
        Assert.Equal(5, RectangleColoring.Assign(Singles100(), 5).Distinct().Count());
    }

    [Theory]
    [InlineData(4)]
    [InlineData(0)]
    [InlineData(-1)]
    public void Too_few_colors_throws(int colors)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RectangleColoring.Assign(Example(), colors));
    }

    [Fact]
    public void Null_map_throws()
    {
        Assert.Throws<ArgumentNullException>(() => RectangleColoring.Assign(null!, 5));
    }

    // The least-used rule gives the five leaves five different colours, so the hub has none left
    [Fact]
    public void Stuck_rectangle_is_freed_by_a_swap()
    {
        int[][] neighbours = [[5], [5], [5], [5], [5], [0, 1, 2, 3, 4]];

        AssertProper(neighbours, RectangleColoring.Assign(neighbours, [0, 1, 2, 3, 4, 5], 5), 5);
    }

    // The leaves get 0-4, then 6 gets 1 and 7 gets 0, so the {0, 1} chain from leaf 0 reaches leaf 1.
    // Swapping that chain would free nothing, so leaf 1, 6 and 7 must keep their colours.
    [Fact]
    public void Swap_skips_a_pair_whose_chain_connects()
    {
        int[][] neighbours = [[5, 6], [5, 7], [5], [5], [5], [0, 1, 2, 3, 4], [0, 7], [6, 1]];

        var colors = RectangleColoring.Assign(neighbours, [0, 1, 2, 3, 4, 6, 7, 5], 5);

        AssertProper(neighbours, colors, 5);
        Assert.Equal((1, 1, 0), (colors[1], colors[6], colors[7]));
    }

    [Fact]
    public void Non_planar_graph_throws()
    {
        var neighbours = Enumerable.Range(0, 6)
            .Select(node => Enumerable.Range(0, 6).Where(other => other != node).ToArray())
            .ToArray();

        Assert.Throws<InvalidOperationException>(() => RectangleColoring.Assign(neighbours, [0, 1, 2, 3, 4, 5], 5));
    }

    // Finds neighbours on its own, square by square, rather than trusting the coloring's graph
    private static void AssertProper(Map map, IReadOnlyList<int> colors, int count)
    {
        Assert.Equal(map.Rectangles.Count, colors.Count);
        Assert.All(colors, color => Assert.InRange(color, 0, count - 1));

        var indexOf = map.Rectangles.Select((rectangle, index) => (rectangle.Id, index)).ToDictionary(p => p.Id, p => p.index);
        var clashes = new List<(Cell, Cell)>();
        for (var row = 0; row < map.Rows; row++)
        {
            for (var column = 0; column < map.Columns; column++)
            {
                var cell = new Cell(row, column);
                foreach (var next in new[] { cell.Step(Direction.Right), cell.Step(Direction.Down) })
                {
                    if (map.Contains(next)
                        && map.RegionAt(next) != map.RegionAt(cell)
                        && colors[indexOf[map.RegionAt(next)]] == colors[indexOf[map.RegionAt(cell)]])
                    {
                        clashes.Add((cell, next));
                    }
                }
            }
        }
        Assert.Empty(clashes);
    }

    private static void AssertProper(IReadOnlyList<IReadOnlyList<int>> neighbours, int[] colors, int count)
    {
        Assert.Equal(neighbours.Count, colors.Length);
        Assert.All(colors, color => Assert.InRange(color, 0, count - 1));
        var clashes =
            from node in Enumerable.Range(0, neighbours.Count)
            from other in neighbours[node]
            where colors[node] == colors[other]
            select (node, other);
        Assert.Empty(clashes);
    }

    // Every tiling of the grid by rectangles, filling the first empty square in reading order each time
    private static IEnumerable<int[,]> Tilings(int rows, int columns) => Fill(new int[rows, columns], 1);

    private static IEnumerable<int[,]> Fill(int[,] grid, int nextId)
    {
        if (FirstEmpty(grid) is not { } first)
        {
            yield return (int[,])grid.Clone();
            yield break;
        }

        for (var height = 1; first.Row + height <= grid.GetLength(0); height++)
        {
            for (var width = 1; first.Column + width <= grid.GetLength(1); width++)
            {
                if (!IsEmpty(grid, first, height, width))
                {
                    break;
                }
                Place(grid, first, height, width, nextId);
                foreach (var tiling in Fill(grid, nextId + 1))
                {
                    yield return tiling;
                }
                Place(grid, first, height, width, 0);
            }
        }
    }

    private static Cell? FirstEmpty(int[,] grid)
    {
        for (var row = 0; row < grid.GetLength(0); row++)
        {
            for (var column = 0; column < grid.GetLength(1); column++)
            {
                if (grid[row, column] == 0)
                {
                    return new Cell(row, column);
                }
            }
        }
        return null;
    }

    private static bool IsEmpty(int[,] grid, Cell topLeft, int height, int width)
    {
        for (var row = topLeft.Row; row < topLeft.Row + height; row++)
        {
            for (var column = topLeft.Column; column < topLeft.Column + width; column++)
            {
                if (grid[row, column] != 0)
                {
                    return false;
                }
            }
        }
        return true;
    }

    private static void Place(int[,] grid, Cell topLeft, int height, int width, int id)
    {
        for (var row = topLeft.Row; row < topLeft.Row + height; row++)
        {
            for (var column = topLeft.Column; column < topLeft.Column + width; column++)
            {
                grid[row, column] = id;
            }
        }
    }

    // A tiling whose only 1x1 square is [0, 0] can't make a valid map, because the start can't be below or right of the end
    private static Cell? FirstSingleAfterOrigin(int[,] regions)
    {
        var sizes = regions.Cast<int>().GroupBy(id => id).ToDictionary(g => g.Key, g => g.Count());
        for (var row = 0; row < regions.GetLength(0); row++)
        {
            for (var column = 0; column < regions.GetLength(1); column++)
            {
                if ((row, column) != (0, 0) && sizes[regions[row, column]] == 1)
                {
                    return new Cell(row, column);
                }
            }
        }
        return null;
    }
}
