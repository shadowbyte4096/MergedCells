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
        var bounds = RegionBounds.Measure(regions, out var firstSeenOrder);
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

    private static string Describe(Cell cell) => $"[{cell.Row}, {cell.Column}]";
}
