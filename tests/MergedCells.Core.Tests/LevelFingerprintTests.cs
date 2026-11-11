using AutoFixture.Xunit3;

namespace MergedCells.Core.Tests;

public class LevelFingerprintTests
{
    private static int Positive(int value) => Math.Abs(value % 1000) + 1;

    private static Map ExampleWith(int[,] regions) => Map.Create(regions, TestMaps.ExampleStart, TestMaps.ExampleEnd);

    // Both computed outside the code under test (PowerShell's BigInteger, in the planning session), so they pin the
    // algorithm itself: a change here would make every saved best count as a different level
    [Fact]
    public void Example_level_fingerprint_is_pinned()
    {
        Assert.Equal("c28a875eb682c187", LevelFingerprint.Of(TestMaps.Example(), 3));
    }

    [Fact]
    public void Example_with_par_4_fingerprint_is_pinned()
    {
        Assert.Equal("62afd3245b0a9990", LevelFingerprint.Of(TestMaps.Example(), 4));
    }

    // Computed outside the code under test, like the plain pins: the plain values, then 1, then the 16 tile codes
    [Fact]
    public void Example_with_a_wall_fingerprint_is_pinned()
    {
        var map = Map.Create(TestMaps.ExampleRegions(), TestMaps.ExampleStart, TestMaps.ExampleEnd, TestMaps.Walls(4, 4, (1, 1)));

        Assert.Equal("5519c22f56523c17", LevelFingerprint.Of(map, 3));
    }

    // Computed outside the code under test too. The example is square with its start and end on the diagonal, so it
    // can't tell rows from columns; this map has 2 rows and 3 columns, and each row has a start or end off the diagonal
    [Theory]
    [InlineData(0, 0, "9595701f6d6c9037")]
    [InlineData(1, 0, "8d3b24c0e00797a6")]
    public void Non_square_fingerprint_is_pinned(int startRow, int startColumn, string expected)
    {
        var map = Map.Create(new[,] { { 1, 1, 2 }, { 3, 3, 4 } }, new(startRow, startColumn), new(1, 2));

        Assert.Equal(expected, LevelFingerprint.Of(map, 2));
    }

    [Theory]
    [AutoData]
    public void Tiles_with_no_walls_keep_the_plain_fingerprint(int par)
    {
        var map = Map.Create(TestMaps.ExampleRegions(), TestMaps.ExampleStart, TestMaps.ExampleEnd, TestMaps.Walls(4, 4));

        Assert.Equal(LevelFingerprint.Of(TestMaps.Example(), Positive(par)), LevelFingerprint.Of(map, Positive(par)));
    }

    [Theory]
    [AutoData]
    public void Adding_a_wall_changes_the_fingerprint(int par)
    {
        var map = Map.Create(TestMaps.ExampleRegions(), TestMaps.ExampleStart, TestMaps.ExampleEnd, TestMaps.Walls(4, 4, (1, 1)));

        Assert.NotEqual(LevelFingerprint.Of(TestMaps.Example(), Positive(par)), LevelFingerprint.Of(map, Positive(par)));
    }

    [Theory]
    [AutoData]
    public void Moving_a_wall_changes_the_fingerprint(int par)
    {
        var first = Map.Create(TestMaps.ExampleRegions(), TestMaps.ExampleStart, TestMaps.ExampleEnd, TestMaps.Walls(4, 4, (1, 1)));
        var second = Map.Create(TestMaps.ExampleRegions(), TestMaps.ExampleStart, TestMaps.ExampleEnd, TestMaps.Walls(4, 4, (1, 2)));

        Assert.NotEqual(LevelFingerprint.Of(first, Positive(par)), LevelFingerprint.Of(second, Positive(par)));
    }

    [Theory]
    [AutoData]
    public void Sticky_square_and_a_wall_on_the_same_square_give_different_fingerprints(int par)
    {
        var sticky = Map.Create(TestMaps.ExampleRegions(), TestMaps.ExampleStart, TestMaps.ExampleEnd, TestMaps.Sticky(4, 4, (1, 1)));
        var wall = Map.Create(TestMaps.ExampleRegions(), TestMaps.ExampleStart, TestMaps.ExampleEnd, TestMaps.Walls(4, 4, (1, 1)));

        Assert.NotEqual(LevelFingerprint.Of(wall, Positive(par)), LevelFingerprint.Of(sticky, Positive(par)));
    }

    [Theory]
    [AutoData]
    public void Trap_wall_sticky_and_plain_squares_give_different_fingerprints(int par)
    {
        var fingerprints = new[] { TestMaps.Traps(4, 4, (1, 1)), TestMaps.Walls(4, 4, (1, 1)), TestMaps.Sticky(4, 4, (1, 1)), null }
            .Select(tiles => LevelFingerprint.Of(
                Map.Create(TestMaps.ExampleRegions(), TestMaps.ExampleStart, TestMaps.ExampleEnd, tiles), Positive(par)));

        Assert.Equal(4, fingerprints.Distinct().Count());
    }

    [Theory]
    [AutoData]
    public void Adding_a_sticky_square_changes_the_fingerprint(int par)
    {
        var map = Map.Create(TestMaps.ExampleRegions(), TestMaps.ExampleStart, TestMaps.ExampleEnd, TestMaps.Sticky(4, 4, (1, 1)));

        Assert.NotEqual(LevelFingerprint.Of(TestMaps.Example(), Positive(par)), LevelFingerprint.Of(map, Positive(par)));
    }

    public static TheoryData<string> MapNames => new() { "example", "rowStrips", "sketch" };

    private static Map Named(string name) => name switch
    {
        "example" => TestMaps.Example(),
        "rowStrips" => TestMaps.RowStrips100(),
        _ => TestMaps.Sketch(),
    };

    [Theory]
    [MemberData(nameof(MapNames))]
    public void Fingerprint_is_16_lowercase_hex_digits(string name)
    {
        var fingerprint = LevelFingerprint.Of(Named(name), 1);

        Assert.Equal(16, fingerprint.Length);
        Assert.All(fingerprint, c => Assert.True(c is >= '0' and <= '9' or >= 'a' and <= 'f', $"'{c}' isn't a lowercase hex digit"));
    }

    [Fact]
    public void Renumbered_regions_keep_the_fingerprint()
    {
        var regions = TestMaps.ExampleRegions();
        for (var row = 0; row < regions.GetLength(0); row++)
        {
            for (var column = 0; column < regions.GetLength(1); column++)
            {
                regions[row, column] = regions[row, column] * 7 + 100;
            }
        }

        Assert.Equal(LevelFingerprint.Of(TestMaps.Example(), 3), LevelFingerprint.Of(ExampleWith(regions), 3));
    }

    public static TheoryData<string> Changes => new() { "shape", "start", "end", "par", "size" };

    // Each pair differs in exactly the named way
    private static ((Map Map, int Par) A, (Map Map, int Par) B) Pair(string change)
    {
        switch (change)
        {
            case "shape":
                var reshaped = TestMaps.ExampleRegions();
                reshaped[0, 1] = 2;
                return ((TestMaps.Example(), 3), (ExampleWith(reshaped), 3));
            case "start":
                return ((TestMaps.Example(), 3), (Map.Create(TestMaps.ExampleRegions(), new(1, 0), TestMaps.ExampleEnd), 3));
            case "end":
                return ((Map.Create(TestMaps.AllSingles3x3(), new(0, 0), new(2, 2)), 3),
                    (Map.Create(TestMaps.AllSingles3x3(), new(0, 0), new(2, 1)), 3));
            case "par":
                return ((TestMaps.Example(), 3), (TestMaps.Example(), 4));
            default:
                return ((Map.Create(TestMaps.AllSingles3x3(), new(0, 0), new(2, 2)), 3),
                    (Map.Create(TestMaps.Grid("ABCD", "EFGH", "IJKL"), new(0, 0), new(2, 2)), 3));
        }
    }

    [Theory]
    [MemberData(nameof(Changes))]
    public void Change_that_alters_play_changes_the_fingerprint(string change)
    {
        var (a, b) = Pair(change);

        Assert.NotEqual(LevelFingerprint.Of(a.Map, a.Par), LevelFingerprint.Of(b.Map, b.Par));
    }

    [Theory]
    [AutoData]
    public void Level_id_name_and_chapter_do_not_change_the_fingerprint(
        int firstId, string firstName, int firstChapter, int secondId, string secondName, int secondChapter)
    {
        var first = new Level(Positive(firstId), firstName, Positive(firstChapter), TestMaps.Example(), 3);
        var second = new Level(Positive(secondId), secondName, Positive(secondChapter), TestMaps.Example(), 3);

        Assert.Equal(LevelFingerprint.Of(first), LevelFingerprint.Of(second));
    }

    [Fact]
    public void Level_fingerprint_is_its_map_and_par_fingerprint()
    {
        var level = new Level(4, "Four", 1, TestMaps.Example(), 3);

        Assert.Equal(LevelFingerprint.Of(TestMaps.Example(), 3), LevelFingerprint.Of(level));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Par_below_1_throws(int par)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LevelFingerprint.Of(TestMaps.Example(), par));
    }

    [Fact]
    public void Null_map_throws()
    {
        Assert.Throws<ArgumentNullException>(() => LevelFingerprint.Of((Map)null!, 3));
    }

    [Fact]
    public void Null_level_throws()
    {
        Assert.Throws<ArgumentNullException>(() => LevelFingerprint.Of((Level)null!));
    }
}
