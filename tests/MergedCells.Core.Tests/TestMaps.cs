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

    // Every row is one strip except the 1x1 end at [99, 99]
    public static Map RowStrips100()
    {
        var regions = new int[100, 100];
        for (var row = 0; row < 100; row++)
        {
            for (var column = 0; column < 100; column++)
            {
                regions[row, column] = row;
            }
        }
        regions[99, 99] = 100;
        return Map.Create(regions, new(0, 0), new(99, 99));
    }

    public static Map Singles100()
    {
        var regions = new int[100, 100];
        for (var row = 0; row < 100; row++)
        {
            for (var column = 0; column < 100; column++)
            {
                regions[row, column] = row * 100 + column;
            }
        }
        return Map.Create(regions, new(0, 0), new(99, 99));
    }

    // The developer's sketch level from the chunk 4 spec
    public static Map Sketch() => Map.Create(
        Grid("SAAAAA", "BCCDDD", "BCCDDD", "BCCEEE", "BCCFFF", "BGHFFF", "IJHKLM", "IJHOOO", "QQQOOO", "PPPPPZ"),
        new(0, 0),
        new(9, 5));

    // X touches all of A, B, C, D and F, which form a ring of five, so the map needs four colours
    public static Map OddWheel() => Map.Create(Grid("ABCC", "AXXD", "AFFD"), new(0, 0), new(0, 1));

    // A trap at [0, 3] in the top lane: Right from the start stops on [0, 1], and Right again is caught
    public static Map TrappedLane() => Map.Create(Grid("ABBBB", "CCCCZ"), new(0, 0), new(1, 4), Traps(2, 5, (0, 3)));

    // A trap on [0, 1]: Right from the start is caught after one square
    public static Map TrapBesideTheStart() => Map.Create(Grid("AAAAA", "BCDEF"), new(0, 0), new(1, 2), Traps(2, 5, (0, 1)));

    // Level 30's layout: right along the top runs over the trap, so par is 3 with it and 2 without
    public static Map TrapTutorial() => Map.Create(Grid("AAAAB", "CCCCB", "DDDDE"), new(0, 0), new(2, 4), Traps(3, 5, (0, 2)));

    // Level 30's layout with two traps in the top lane. Together they close the quick route (par 3, not 2), but each
    // catches the slide along the top without the other, so neither changes anything alone
    public static Map CoveringTraps() => Map.Create(Grid("AAAAB", "CCCCB", "DDDDE"), new(0, 0), new(2, 4), Traps(3, 5, (0, 2), (0, 3)));

    // Right from the start enters [0, 2], comes out of [1, 4] and runs to the edge at [1, 5]
    public static Map TeleporterLane() =>
        Map.Create(Grid("AAAAAA", "BBBBBB", "CCCCCZ"), new(0, 0), new(2, 5), teleporters: [Pair(0, 2, 1, 4)]);

    // Right from the start enters [0, 4], comes out of [0, 1] behind it, and stops on the start's region boundary
    public static Map RoundTrip() =>
        Map.Create(Grid("AABBBB", "CCCCCZ"), new(0, 2), new(1, 5), teleporters: [Pair(0, 1, 0, 4)]);

    // Left or Right from the start enters one teleporter, comes out of the other behind the player, and goes round for ever
    public static Map LoopingLane() =>
        Map.Create(Grid("AAAAA", "BBBBZ"), new(0, 2), new(1, 4), teleporters: [Pair(0, 1, 0, 3)]);

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

    // A tile grid with walls on the given squares
    public static Tile[,] Walls(int rows, int columns, params (int Row, int Column)[] walls) => Tiles(rows, columns, Tile.Wall, walls);

    public static Tile[,] Sticky(int rows, int columns, params (int Row, int Column)[] sticky) => Tiles(rows, columns, Tile.Sticky, sticky);

    public static Tile[,] Traps(int rows, int columns, params (int Row, int Column)[] traps) => Tiles(rows, columns, Tile.Trap, traps);

    public static TeleporterPair Pair(int firstRow, int firstColumn, int secondRow, int secondColumn) =>
        new(new(firstRow, firstColumn), new(secondRow, secondColumn));

    private static Tile[,] Tiles(int rows, int columns, Tile tile, (int Row, int Column)[] squares)
    {
        var tiles = new Tile[rows, columns];
        foreach (var (row, column) in squares)
        {
            tiles[row, column] = tile;
        }
        return tiles;
    }
}
