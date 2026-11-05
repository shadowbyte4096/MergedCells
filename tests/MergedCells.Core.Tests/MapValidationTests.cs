using AutoFixture;
using static MergedCells.Core.MapProblemKind;
using static MergedCells.Core.Tests.TestMaps;

namespace MergedCells.Core.Tests;

public class MapValidationTests
{
    private static MapProblemKind[] Kinds(int[,] regions, Cell start, Cell end) =>
        Map.Validate(regions, start, end).Select(p => p.Kind).ToArray();

    public static TheoryData<int[,], Cell, Cell> ValidMaps => new()
    {
        { ExampleRegions(), new(0, 0), new(3, 3) }, // section 7 example
        { Grid("AB", "CD"), new(0, 0), new(1, 1) }, // every square its own region
        { Grid("AAAB"), new(0, 0), new(0, 3) }, // one row
        { Grid("A", "A", "B"), new(0, 0), new(2, 0) }, // one column
        { new[,] { { -5, -5, 40 }, { 7, 7, 40 }, { 7, 7, 0 } }, new(0, 0), new(2, 2) }, // negative and non-contiguous ids
        { new[,] { { int.MinValue, int.MaxValue }, { int.MinValue, 0 } }, new(0, 0), new(1, 1) }, // extreme ids
        { AllSingles3x3(), new(1, 1), new(2, 2) }, // start off the top-left corner
    };

    [Theory]
    [MemberData(nameof(ValidMaps))]
    public void Valid_map_has_no_problems(int[,] regions, Cell start, Cell end)
    {
        Assert.Empty(Map.Validate(regions, start, end));
    }

    [Fact]
    public void Generated_region_ids_are_valid()
    {
        var ids = new Fixture().CreateMany<int>(8).ToList();
        var regions = Relabel(ExampleRegions(), ids);

        Assert.Empty(Map.Validate(regions, ExampleStart, ExampleEnd));
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(3, 0)]
    [InlineData(0, 0)]
    public void Empty_grid_reports_only_EmptyGrid(int rows, int columns)
    {
        Assert.Equal(new[] { EmptyGrid }, Kinds(new int[rows, columns], new(0, 0), new(0, 0)));
    }

    public static TheoryData<int[,], Cell, Cell> IrregularRegions => new()
    {
        { Grid("ABB", "AAC"), new(0, 0), new(1, 2) }, // L-shape
        { Grid("AAAB", "ACAB", "AAAB", "DDDE"), new(0, 0), new(3, 3) }, // ring
        { Grid("ABBE", "AABE", "CAAE", "DDDF"), new(0, 0), new(3, 3) }, // zigzag
    };

    [Theory]
    [MemberData(nameof(IrregularRegions))]
    public void Irregular_connected_region_is_valid(int[,] regions, Cell start, Cell end)
    {
        Assert.Empty(Map.Validate(regions, start, end));
    }

    public static TheoryData<int[,], Cell, Cell> DisconnectedRegions => new()
    {
        { Grid("ABA", "CCD"), new(0, 0), new(1, 2) }, // two pieces
        { Grid("ABD", "CAE"), new(0, 0), new(1, 2) }, // touching itself only at a corner
    };

    [Theory]
    [MemberData(nameof(DisconnectedRegions))]
    public void Region_in_more_than_one_piece_is_reported(int[,] regions, Cell start, Cell end)
    {
        Assert.Equal(new[] { RegionNotConnected }, Kinds(regions, start, end));
    }

    [Fact]
    public void Each_bad_region_is_reported_once_in_order_of_first_appearance()
    {
        var problems = Map.Validate(new[,] { { 5, 6, 5 }, { 6, 7, 7 }, { 8, 8, 9 } }, new(0, 0), new(2, 2));

        Assert.Collection(problems,
            p => Assert.StartsWith("Region 5 ", p.Message),
            p => Assert.StartsWith("Region 6 ", p.Message));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(4, 0)]
    [InlineData(0, 4)]
    public void Start_outside_the_map_is_reported(int row, int column)
    {
        Assert.Contains(StartOutsideMap, Kinds(ExampleRegions(), new(row, column), ExampleEnd));
    }

    [Theory]
    [InlineData(4, 3)]
    [InlineData(3, 4)]
    [InlineData(9, 9)]
    [InlineData(-1, 3)]
    [InlineData(3, -1)]
    public void End_outside_the_map_is_reported_without_an_end_region_problem(int row, int column)
    {
        var kinds = Kinds(ExampleRegions(), ExampleStart, new(row, column));

        Assert.Contains(EndOutsideMap, kinds);
        Assert.DoesNotContain(EndNotSingleSquare, kinds);
    }

    public static TheoryData<int[,], Cell> LargeEndRegions => new()
    {
        { Grid("AAAA", "BBBB"), new(0, 2) }, // strip
        { Grid("AABB", "CCBB"), new(1, 3) }, // 2x2 block
    };

    [Theory]
    [MemberData(nameof(LargeEndRegions))]
    public void End_in_a_larger_region_is_reported(int[,] regions, Cell end)
    {
        Assert.Equal(new[] { EndNotSingleSquare }, Kinds(regions, new(0, 0), end));
    }

    [Theory]
    [InlineData(0, 2, 2, 0, StartRightOfEnd)]
    [InlineData(2, 0, 0, 2, StartBelowEnd)]
    [InlineData(1, 1, 1, 1, StartOnEnd)]
    public void Start_placement_relative_to_end_is_checked(
        int startRow, int startColumn, int endRow, int endColumn, MapProblemKind expected)
    {
        Assert.Equal(new[] { expected }, Kinds(AllSingles3x3(), new(startRow, startColumn), new(endRow, endColumn)));
    }

    // An end can't be both off the map and in a larger region, and a start on the end can't also be beside it,
    // so it takes several maps to put every kind in order
    public static TheoryData<int[,], Cell, Cell, MapProblemKind[]> ProblemCombinations => new()
    {
        {
            Grid("ABAC", "DDDC", "EEEC", "FFFG"), new(5, 5), new(3, 3),
            new[] { RegionNotConnected, StartOutsideMap, StartRightOfEnd, StartBelowEnd }
        },
        {
            Grid("ABAC", "DDDC", "EEEC", "FFFG"), new(9, 9), new(4, 4),
            new[] { RegionNotConnected, StartOutsideMap, EndOutsideMap, StartRightOfEnd, StartBelowEnd }
        },
        {
            Grid("AAAA", "BCBD"), new(5, 5), new(0, 2),
            new[] { RegionNotConnected, StartOutsideMap, EndNotSingleSquare, StartRightOfEnd, StartBelowEnd }
        },
        {
            Grid("AAAA", "BCBD"), new(0, 2), new(0, 2),
            new[] { RegionNotConnected, EndNotSingleSquare, StartOnEnd }
        },
    };

    [Theory]
    [MemberData(nameof(ProblemCombinations))]
    public void Every_problem_is_reported_in_kind_order(int[,] regions, Cell start, Cell end, MapProblemKind[] expected)
    {
        Assert.Equal(expected, Kinds(regions, start, end));
    }

    [Fact]
    public void Null_grid_throws()
    {
        Assert.Throws<ArgumentNullException>(() => Map.Validate(null!, new(0, 0), new(1, 1)));
    }
}
