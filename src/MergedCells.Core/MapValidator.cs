namespace MergedCells.Core;

internal static class MapValidator
{
    public static IReadOnlyList<MapProblem> Validate(int[,] regions, Cell start, Cell end)
    {
        ArgumentNullException.ThrowIfNull(regions);
        var rows = regions.GetLength(0);
        var columns = regions.GetLength(1);
        if (rows == 0 || columns == 0)
        {
            return [new MapProblem(MapProblemKind.EmptyGrid, $"The grid is {rows}x{columns}; it needs at least one row and one column.")];
        }

        var problems = new List<MapProblem>();
        var bounds = MeasureRegions(regions, out var firstSeenOrder);
        AddRegionProblems(problems, bounds, firstSeenOrder);
        AddStartAndEndProblems(problems, regions, bounds, start, end);
        return problems;
    }

    private static void AddRegionProblems(List<MapProblem> problems, Dictionary<int, RegionBounds> bounds, List<int> firstSeenOrder)
    {
        foreach (var id in firstSeenOrder)
        {
            var region = bounds[id];
            if (region.Count != region.Area)
            {
                problems.Add(new MapProblem(
                    MapProblemKind.RegionNotRectangle,
                    $"Region {id} is not a rectangle: its squares span rows {region.MinRow}-{region.MaxRow} and columns {region.MinColumn}-{region.MaxColumn}."));
            }
        }
    }

    private static void AddStartAndEndProblems(
        List<MapProblem> problems, int[,] regions, Dictionary<int, RegionBounds> bounds, Cell start, Cell end)
    {
        var rows = regions.GetLength(0);
        var columns = regions.GetLength(1);
        var endInside = Map.IsInside(end, rows, columns);
        if (!Map.IsInside(start, rows, columns))
        {
            problems.Add(new MapProblem(MapProblemKind.StartOutsideMap, $"The start {Describe(start)} is outside the {rows}x{columns} grid."));
        }
        if (!endInside)
        {
            problems.Add(new MapProblem(MapProblemKind.EndOutsideMap, $"The end {Describe(end)} is outside the {rows}x{columns} grid."));
        }
        if (endInside && bounds[regions[end.Row, end.Column]].Count > 1)
        {
            problems.Add(new MapProblem(
                MapProblemKind.EndNotSingleSquare,
                $"The end {Describe(end)} shares region {regions[end.Row, end.Column]} with other squares; it must be its own 1x1 rectangle."));
        }
        if (start.Column > end.Column)
        {
            problems.Add(new MapProblem(MapProblemKind.StartRightOfEnd, $"The start {Describe(start)} is to the right of the end {Describe(end)}."));
        }
        if (start.Row > end.Row)
        {
            problems.Add(new MapProblem(MapProblemKind.StartBelowEnd, $"The start {Describe(start)} is below the end {Describe(end)}."));
        }
        if (start == end)
        {
            problems.Add(new MapProblem(MapProblemKind.StartOnEnd, $"The start and the end are the same square {Describe(start)}."));
        }
    }

    private static Dictionary<int, RegionBounds> MeasureRegions(int[,] regions, out List<int> firstSeenOrder)
    {
        var bounds = new Dictionary<int, RegionBounds>();
        firstSeenOrder = [];
        for (var row = 0; row < regions.GetLength(0); row++)
        {
            for (var column = 0; column < regions.GetLength(1); column++)
            {
                var id = regions[row, column];
                if (bounds.TryGetValue(id, out var region))
                {
                    bounds[id] = region.Include(row, column);
                }
                else
                {
                    bounds[id] = new RegionBounds(row, row, column, column, 1);
                    firstSeenOrder.Add(id);
                }
            }
        }
        return bounds;
    }

    private static string Describe(Cell cell) => $"[{cell.Row}, {cell.Column}]";

    private readonly record struct RegionBounds(int MinRow, int MaxRow, int MinColumn, int MaxColumn, int Count)
    {
        public int Area => (MaxRow - MinRow + 1) * (MaxColumn - MinColumn + 1);

        public RegionBounds Include(int row, int column) => new(
            Math.Min(MinRow, row),
            Math.Max(MaxRow, row),
            Math.Min(MinColumn, column),
            Math.Max(MaxColumn, column),
            Count + 1);
    }
}
