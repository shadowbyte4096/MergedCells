namespace MergedCells.Core.Tests;

public class ShopBoardsTests
{
    [Fact]
    public void Preview_shows_one_of_each_twist()
    {
        var map = ShopBoards.Preview;
        var cells = Enumerable.Range(0, map.Rows).SelectMany(row => Enumerable.Range(0, map.Columns).Select(column => new Cell(row, column)));

        Assert.Equal(
            (1, 1, 1),
            (cells.Count(cell => map.TileAt(cell) == Tile.Wall), cells.Count(cell => map.TileAt(cell) == Tile.Trap), map.Teleporters.Count));
    }

    [Fact]
    public void Preview_is_six_by_six_from_corner_to_corner()
    {
        var map = ShopBoards.Preview;

        Assert.Equal((6, 6, new Cell(0, 0), new Cell(5, 5)), (map.Rows, map.Columns, map.Start, map.End));
    }

    [Fact]
    public void Preview_player_is_on_a_plain_square_off_the_start()
    {
        var map = ShopBoards.Preview;
        var player = ShopBoards.PreviewPlayer;

        Assert.Equal((Tile.None, false, false), (map.TileAt(player), player == map.Start, map.Teleporters.Any(pair => pair.First == player || pair.Second == player)));
    }

    [Fact]
    public void Close_up_is_plain_with_the_player_in_the_middle()
    {
        var map = ShopBoards.CloseUp;

        Assert.Equal((3, 3, false, 0, new Cell(1, 1)), (map.Rows, map.Columns, map.HasTiles, map.Teleporters.Count, ShopBoards.CloseUpPlayer));
    }
}
