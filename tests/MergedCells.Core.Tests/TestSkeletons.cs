namespace MergedCells.Core.Tests;

// Skeletons the generator and climber tests share
internal static class TestSkeletons
{
    public static readonly Cell GapEnd = new(6, 6);

    public static readonly int[] GapWallColumns = [0, 1, 2, 3, 5, 6];

    // 7x7: block 1 fixed along row 0 from column 2, and a wall line across row 3 with one gap at column 4 (the Gap's line)
    public static int?[,] GapRegions()
    {
        var regions = new int?[7, 7];
        for (var column = 2; column < 7; column++)
        {
            regions[0, column] = 1;
        }
        return regions;
    }

    public static Tile?[,] GapTiles()
    {
        var tiles = new Tile?[7, 7];
        foreach (var column in GapWallColumns)
        {
            tiles[3, column] = Tile.Wall;
        }
        return tiles;
    }

    public static MapSkeleton Gap(Tile?[,]? tiles = null, IReadOnlyList<Cell>? starts = null) =>
        new(GapRegions(), tiles ?? GapTiles(), GapEnd, starts ?? []);

    // 10x10, with something of every kind fixed: block 1 along row 0 from column 3, block 2 a 2x2 at [5, 1], walls on row
    // 4 from column 0 to 6, column 9 kept plain from row 1 to 8, and the start in the top-left 2x2
    public static MapSkeleton Fenced()
    {
        var regions = new int?[10, 10];
        for (var column = 3; column < 10; column++)
        {
            regions[0, column] = 1;
        }
        foreach (var (row, column) in new[] { (5, 1), (5, 2), (6, 1), (6, 2) })
        {
            regions[row, column] = 2;
        }
        var tiles = new Tile?[10, 10];
        for (var column = 0; column <= 6; column++)
        {
            tiles[4, column] = Tile.Wall;
        }
        for (var row = 1; row <= 8; row++)
        {
            tiles[row, 9] = Tile.None;
        }
        return new MapSkeleton(regions, tiles, new(9, 9), [new(0, 0), new(0, 1), new(1, 0), new(1, 1)]);
    }

    public static IEnumerable<Cell> Squares(int size) =>
        Enumerable.Range(0, size * size).Select(index => new Cell(index / size, index % size));
}
