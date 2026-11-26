namespace MergedCells.Core;

/// <summary>
/// The shop's two small boards: <see cref="Preview"/>, which shows a theme on every board piece (a wall, a trap, a
/// teleporter pair, the end and the start), and <see cref="CloseUp"/>, a plain 3×3 that shows a look up close.
/// </summary>
public static class ShopBoards
{
    public static Map Preview { get; } = Map.Create(
        new[,]
        {
            { 1, 1, 1, 2, 3, 3 },
            { 4, 5, 5, 2, 3, 3 },
            { 4, 5, 5, 2, 6, 7 },
            { 4, 8, 8, 8, 6, 7 },
            { 9, 9, 10, 10, 6, 7 },
            { 11, 11, 10, 10, 12, 13 },
        },
        new Cell(0, 0),
        new Cell(5, 5),
        Tiles(6, (new Cell(0, 1), Tile.Wall), (new Cell(2, 2), Tile.Trap)),
        [new TeleporterPair(new Cell(1, 4), new Cell(4, 1))]);

    public static Cell PreviewPlayer { get; } = new(3, 3);

    public static Map CloseUp { get; } = Map.Create(
        new[,]
        {
            { 1, 1, 2 },
            { 3, 4, 2 },
            { 3, 5, 6 },
        },
        new Cell(0, 0),
        new Cell(2, 2));

    public static Cell CloseUpPlayer { get; } = new(1, 1);

    private static Tile[,] Tiles(int side, params (Cell Cell, Tile Tile)[] placed)
    {
        var tiles = new Tile[side, side];
        foreach (var (cell, tile) in placed)
        {
            tiles[cell.Row, cell.Column] = tile;
        }
        return tiles;
    }
}
