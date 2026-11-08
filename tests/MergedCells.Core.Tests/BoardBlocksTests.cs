namespace MergedCells.Core.Tests;

// Block layouts depend on the exact map and block sizes, so they're literals
public class BoardBlocksTests
{
    [Fact]
    public void Counts_round_up_for_partial_blocks()
    {
        var blocks = new BoardBlocks(TestMaps.Sketch(), 4);

        Assert.Equal((3, 2, 6, new CellRange(8, 4, 9, 5)), (blocks.Rows, blocks.Columns, blocks.Count, blocks.Area(5)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(8)]
    public void Every_region_is_in_the_block_holding_its_bounding_top_left(int size)
    {
        var map = TestMaps.Sketch();
        var blocks = new BoardBlocks(map, size);

        var placed = Enumerable.Range(0, blocks.Count)
            .SelectMany(block => blocks.Regions(block).Select(region => (Block: block, Region: region)))
            .ToList();

        Assert.Equal(Enumerable.Range(0, map.Rectangles.Count), placed.Select(entry => entry.Region).Order());
        Assert.All(placed, entry => Assert.True(Contains(blocks.Area(entry.Block), map.Rectangles[entry.Region].TopLeft)));
    }

    [Fact]
    public void Regions_keep_map_order()
    {
        var blocks = new BoardBlocks(TestMaps.Sketch(), 3);

        Assert.All(Enumerable.Range(0, blocks.Count), block => Assert.Equal(blocks.Regions(block).Order(), blocks.Regions(block)));
    }

    // R's first square is (0, 4), in block column 1, but its bounding rectangle starts at (0, 1), in block 0. Placed by
    // its first square, R would come after C, and its hole would paint over C
    [Fact]
    public void Ring_is_drawn_before_the_regions_inside_it()
    {
        var map = Map.Create(
            TestMaps.Grid("AAAARRR", "ARRRRBR", "ARCCCBR", "ARCCCBR", "ARRRRRR", "DDDDDDE"), new Cell(0, 0), new Cell(5, 6));
        var blocks = new BoardBlocks(map, 4);
        var rank = Enumerable.Range(0, blocks.Count)
            .SelectMany(block => blocks.Regions(block).Select(region => (Region: region, Rank: (block, region))))
            .ToDictionary(entry => map.Rectangles[entry.Region].Id, entry => entry.Rank);

        Assert.True(rank['R'].CompareTo(rank['B']) < 0 && rank['R'].CompareTo(rank['C']) < 0);
    }

    [Fact]
    public void Overlapping_lists_the_blocks_a_range_touches()
    {
        var blocks = new BoardBlocks(TestMaps.RowStrips100(), 8);

        Assert.Equal([0, 1, 13, 14], blocks.Overlapping(new CellRange(7, 0, 9, 8)));
    }

    // Every row strip starts in column 0, so only block column 0 draws anything, but its strips reach every column
    [Fact]
    public void Drawing_lists_the_blocks_whose_regions_reach_into_a_range()
    {
        var blocks = new BoardBlocks(TestMaps.RowStrips100(), 8);

        Assert.Equal([0, 13], blocks.Drawing(new CellRange(7, 50, 9, 60)));
    }

    [Fact]
    public void Build_order_puts_overlapping_blocks_first_then_by_distance()
    {
        var blocks = new BoardBlocks(TestMaps.Sketch(), 4);

        Assert.Equal([3, 0, 1, 2, 4, 5], blocks.BuildOrder(new CellRange(5, 4, 6, 5)));
    }

    // 13 blocks across: after block 0 come the blocks one away (1, 13, 14), then those two away, in reading order
    [Fact]
    public void Build_order_counts_distance_in_blocks()
    {
        var blocks = new BoardBlocks(TestMaps.RowStrips100(), 8);

        Assert.Equal([0, 1, 13, 14, 2, 15, 26, 27, 28], blocks.BuildOrder(new CellRange(0, 0, 1, 1)).Take(9));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Block_size_below_1_throws(int size)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BoardBlocks(TestMaps.Sketch(), size));
    }

    private static bool Contains(CellRange range, Cell cell) =>
        cell.Row >= range.Top && cell.Row <= range.Bottom && cell.Column >= range.Left && cell.Column <= range.Right;
}
