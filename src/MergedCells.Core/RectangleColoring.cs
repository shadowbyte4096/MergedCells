namespace MergedCells.Core;

public static class RectangleColoring
{
    public const int MinimumColors = 5;

    /// <summary>
    /// Gives each rectangle a colour index from 0 to <paramref name="colors"/> − 1, one per entry in
    /// <see cref="Map.Rectangles"/> and in the same order. Rectangles that share part of an edge never get the same
    /// index. Five colours are enough for any valid map, and the same map always gets the same indices.
    /// </summary>
    public static IReadOnlyList<int> Assign(Map map, int colors)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentOutOfRangeException.ThrowIfLessThan(colors, MinimumColors);

        var neighbours = Neighbours(map);
        var order = SmallestLast(neighbours);
        order.Reverse();
        return Array.AsReadOnly(Assign(neighbours, order, colors));
    }

    // The test hook: an explicit order reaches the swap path, which real maps rarely hit, and accepts non-planar graphs
    internal static int[] Assign(IReadOnlyList<IReadOnlyList<int>> neighbours, IReadOnlyList<int> order, int colors)
    {
        var color = new int[neighbours.Count];
        Array.Fill(color, -1);
        var uses = new int[colors];
        foreach (var node in order)
        {
            var chosen = LeastUsedFree(neighbours[node], color, uses);
            if (chosen < 0)
            {
                chosen = FreeBySwap(neighbours, node, color, uses);
            }
            color[node] = chosen;
            uses[chosen]++;
        }
        return color;
    }

    private static List<int>[] Neighbours(Map map)
    {
        var indexOf = new Dictionary<int, int>();
        for (var index = 0; index < map.Rectangles.Count; index++)
        {
            indexOf[map.Rectangles[index].Id] = index;
        }

        var neighbours = new List<int>[map.Rectangles.Count];
        for (var index = 0; index < neighbours.Length; index++)
        {
            neighbours[index] = [];
        }
        var seen = new HashSet<(int, int)>();
        for (var row = 0; row < map.Rows; row++)
        {
            for (var column = 0; column < map.Columns; column++)
            {
                var here = indexOf[map.RegionAt(new Cell(row, column))];
                if (column + 1 < map.Columns)
                {
                    Link(here, indexOf[map.RegionAt(new Cell(row, column + 1))]);
                }
                if (row + 1 < map.Rows)
                {
                    Link(here, indexOf[map.RegionAt(new Cell(row + 1, column))]);
                }
            }
        }
        return neighbours;

        void Link(int a, int b)
        {
            if (a != b && seen.Add((Math.Min(a, b), Math.Max(a, b))))
            {
                neighbours[a].Add(b);
                neighbours[b].Add(a);
            }
        }
    }

    // Repeatedly removes the node with the fewest remaining neighbours (lowest index on ties). Colouring in the
    // reverse order then sees at most five coloured neighbours per node, because a planar graph always has a node of
    // degree five or less.
    private static List<int> SmallestLast(IReadOnlyList<IReadOnlyList<int>> neighbours)
    {
        var degree = neighbours.Select(list => list.Count).ToArray();
        var removed = new bool[neighbours.Count];
        var queue = new SortedSet<(int Degree, int Index)>(degree.Select((d, index) => (d, index)));
        var order = new List<int>(neighbours.Count);
        while (queue.Count > 0)
        {
            var next = queue.Min;
            queue.Remove(next);
            removed[next.Index] = true;
            order.Add(next.Index);
            foreach (var other in neighbours[next.Index])
            {
                if (!removed[other])
                {
                    queue.Remove((degree[other], other));
                    degree[other]--;
                    queue.Add((degree[other], other));
                }
            }
        }
        return order;
    }

    private static int LeastUsedFree(IReadOnlyList<int> around, int[] color, int[] uses)
    {
        var taken = new bool[uses.Length];
        foreach (var other in around)
        {
            if (color[other] >= 0)
            {
                taken[color[other]] = true;
            }
        }

        var best = -1;
        for (var candidate = 0; candidate < uses.Length; candidate++)
        {
            if (!taken[candidate] && (best < 0 || uses[candidate] < uses[best]))
            {
                best = candidate;
            }
        }
        return best;
    }

    // Swapping the two colours of a Kempe chain keeps the colouring proper. If the chain from a doesn't reach b, a's
    // colour is left on no neighbour of the node. Planarity guarantees such a pair when five neighbours use five colours.
    private static int FreeBySwap(IReadOnlyList<IReadOnlyList<int>> neighbours, int node, int[] color, int[] uses)
    {
        var colored = neighbours[node].Where(other => color[other] >= 0).ToArray();
        for (var i = 0; i < colored.Length; i++)
        {
            for (var j = i + 1; j < colored.Length; j++)
            {
                var (a, b) = (colored[i], colored[j]);
                var (first, second) = (color[a], color[b]);
                if (first == second)
                {
                    continue;
                }
                var chain = Chain(neighbours, a, first, second, color);
                if (chain.Contains(b))
                {
                    continue;
                }
                foreach (var member in chain)
                {
                    uses[color[member]]--;
                    color[member] = color[member] == first ? second : first;
                    uses[color[member]]++;
                }
                var free = LeastUsedFree(neighbours[node], color, uses);
                if (free >= 0)
                {
                    return free;
                }
            }
        }
        throw new InvalidOperationException($"Node {node} can't be coloured with {uses.Length} colours; the graph isn't planar.");
    }

    private static HashSet<int> Chain(IReadOnlyList<IReadOnlyList<int>> neighbours, int start, int first, int second, int[] color)
    {
        var chain = new HashSet<int> { start };
        var queue = new Queue<int>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            foreach (var next in neighbours[queue.Dequeue()])
            {
                if ((color[next] == first || color[next] == second) && chain.Add(next))
                {
                    queue.Enqueue(next);
                }
            }
        }
        return chain;
    }
}
