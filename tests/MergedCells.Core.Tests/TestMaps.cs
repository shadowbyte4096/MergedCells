namespace MergedCells.Core.Tests;

internal static class TestMaps
{
    public static readonly Cell ExampleStart = new(0, 0);
    public static readonly Cell ExampleEnd = new(3, 3);

    // The example level in section 7 of the design doc, solved by down, down, right
    public static int[,] ExampleRegions() => new[,]
    {
        { 1, 1, 2, 3 },
        { 4, 5, 2, 3 },
        { 4, 5, 6, 6 },
        { 7, 7, 7, 8 },
    };

    public static Map Example() => Map.Create(ExampleRegions(), ExampleStart, ExampleEnd);

    public static int[,] AllSingles3x3() => Grid("ABC", "DEF", "GHI");

    // Draws a region grid as rows of letters, one per square; squares with the same letter share a region.
    // Tests that depend on the ID values themselves use number grids instead.
    public static int[,] Grid(params string[] rows)
    {
        var columns = rows.Length == 0 ? 0 : rows[0].Length;
        var grid = new int[rows.Length, columns];
        for (var row = 0; row < rows.Length; row++)
        {
            if (rows[row].Length != columns)
            {
                throw new ArgumentException($"Row {row} has {rows[row].Length} squares; row 0 has {columns}.", nameof(rows));
            }
            for (var column = 0; column < columns; column++)
            {
                grid[row, column] = rows[row][column];
            }
        }
        return grid;
    }

    // Swaps a layout's 1-based labels for the given ids, keeping its shape
    public static int[,] Relabel(int[,] layout, IReadOnlyList<int> ids)
    {
        var relabelled = new int[layout.GetLength(0), layout.GetLength(1)];
        for (var row = 0; row < layout.GetLength(0); row++)
        {
            for (var column = 0; column < layout.GetLength(1); column++)
            {
                relabelled[row, column] = ids[layout[row, column] - 1];
            }
        }
        return relabelled;
    }
}
