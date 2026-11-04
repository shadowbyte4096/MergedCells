using AutoFixture;
using static MergedCells.Core.Tests.TestMaps;

namespace MergedCells.Core.Tests;

public class MapRectanglesTests
{
    // The example's rectangles, labelled 1 to 8 as in ExampleRegions
    private static readonly Rectangle[] ExampleRectangles =
    [
        new(1, new(0, 0), 1, 2),
        new(2, new(0, 2), 2, 1),
        new(3, new(0, 3), 2, 1),
        new(4, new(1, 0), 2, 1),
        new(5, new(1, 1), 2, 1),
        new(6, new(2, 2), 1, 2),
        new(7, new(3, 0), 1, 3),
        new(8, new(3, 3), 1, 1),
    ];

    private static IEnumerable<(Cell TopLeft, int Rows, int Columns)> Shapes(Map map) =>
        map.Rectangles.Select(r => (r.TopLeft, r.Rows, r.Columns));

    [Fact]
    public void Example_rectangles_in_reading_order()
    {
        Assert.Equal(ExampleRectangles, Example().Rectangles);
    }

    [Fact]
    public void Relabelled_ids_keep_the_shapes()
    {
        var ids = new Fixture().CreateMany<int>(8).ToList();
        var map = Map.Create(Relabel(ExampleRegions(), ids), ExampleStart, ExampleEnd);

        Assert.Equal(ExampleRectangles.Select(r => r with { Id = ids[r.Id - 1] }), map.Rectangles);
    }

    [Fact]
    public void Single_squares_are_one_by_one_in_reading_order()
    {
        var map = Map.Create(AllSingles3x3(), new(0, 0), new(2, 2));
        var expected =
            from row in Enumerable.Range(0, 3)
            from column in Enumerable.Range(0, 3)
            select (new Cell(row, column), 1, 1);

        Assert.Equal(expected, Shapes(map));
    }

    [Fact]
    public void Non_square_map_keeps_rows_and_columns_apart()
    {
        var map = Map.Create(Grid("AAAAB", "CCCCD"), new(0, 0), new(1, 4));

        Assert.Equal(
            [(new Cell(0, 0), 1, 4), (new Cell(0, 4), 1, 1), (new Cell(1, 0), 1, 4), (new Cell(1, 4), 1, 1)],
            Shapes(map));
    }

    [Fact]
    public void Strip_and_end_give_two_rectangles()
    {
        var map = Map.Create(Grid("AAAB"), new(0, 0), new(0, 3));

        Assert.Equal(2, map.Rectangles.Count);
    }

    [Fact]
    public void BottomRight_is_the_last_square()
    {
        var rectangle = new Rectangle(new Fixture().Create<int>(), new(2, 3), 2, 4);

        Assert.Equal(new Cell(3, 6), rectangle.BottomRight);
    }
}
