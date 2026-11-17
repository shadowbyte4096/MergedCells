namespace MergedCells.Core.Tests;

public class LevelPictureTests
{
    private const int Colors = 5;

    private static (bool Vertical, int Row, int Column, int Length)[] Lines(Map map) =>
        LevelPicture.Of(map, Colors).Lines.Select(line => (line.Vertical, line.Row, line.Column, line.Length)).ToArray();

    [Fact]
    public void Fills_are_one_run_per_region_per_row()
    {
        var map = Map.Create(TestMaps.Grid("AAB", "CCZ"), new(0, 0), new(1, 2));

        var runs = LevelPicture.Of(map, Colors).Fills.Select(run => (run.Row, run.Column, run.Length));

        Assert.Equal([(0, 0, 2), (0, 2, 1), (1, 0, 2), (1, 2, 1)], runs);
    }

    [Fact]
    public void Fill_colours_are_rectangle_colourings()
    {
        var map = TestMaps.Example();
        var colors = RectangleColoring.Assign(map, Colors);
        var index = map.Rectangles.Select((rectangle, i) => (rectangle.Id, i)).ToDictionary(pair => pair.Id, pair => pair.i);

        var picture = LevelPicture.Of(map, Colors);

        Assert.All(picture.Fills, run => Assert.Equal(colors[index[map.RegionAt(new(run.Row, run.Column))]], run.Color));
    }

    // Verticals first, column by column top down; then horizontals, row by row left to right
    [Fact]
    public void Lines_merge_into_straight_runs()
    {
        var map = Map.Create(TestMaps.Grid("AAB", "AAZ"), new(0, 0), new(1, 2));

        Assert.Equal([(true, 0, 2, 2), (false, 1, 2, 1)], Lines(map));
    }

    [Fact]
    public void Irregular_region_has_lines_only_where_regions_differ()
    {
        var map = Map.Create(TestMaps.Grid("ABZ", "AAA"), new(0, 0), new(0, 2));

        Assert.Equal([(true, 0, 1, 1), (true, 0, 2, 1), (false, 1, 1, 2)], Lines(map));
    }

    [Fact]
    public void Map_edge_is_not_a_line()
    {
        var map = Map.Create(TestMaps.Grid("ABCDE"), new(0, 0), new(0, 4));

        Assert.Equal([(true, 0, 1, 1), (true, 0, 2, 1), (true, 0, 3, 1), (true, 0, 4, 1)], Lines(map));
    }

    [Fact]
    public void Marks_list_walls_traps_and_both_pair_squares()
    {
        var map = Map.Create(
            TestMaps.Grid("AABC", "DABC", "DEEZ"),
            new(0, 0),
            new(2, 3),
            new Tile[,] { { 0, 0, 0, 0 }, { Tile.Wall, 0, 0, 0 }, { 0, Tile.Trap, 0, 0 } },
            [TestMaps.Pair(0, 2, 1, 3)]);

        var picture = LevelPicture.Of(map, Colors);

        Assert.Equal([new Cell(1, 0)], picture.Walls);
        Assert.Equal([new Cell(2, 1)], picture.Traps);
        Assert.Equal([new Cell(0, 2), new Cell(1, 3)], picture.Teleporters);
    }

    [Fact]
    public void Start_end_and_size_are_the_map_s()
    {
        var map = TestMaps.Example();

        var picture = LevelPicture.Of(map, Colors);

        Assert.Equal((map.Rows, map.Columns, map.Start, map.End), (picture.Rows, picture.Columns, picture.Start, picture.End));
    }
}
