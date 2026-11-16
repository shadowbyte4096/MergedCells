namespace MergedCells.Core.Tests;

public class MapSkeletonTests
{
    // A 4x4 skeleton: block 1 fixed on [0, 2] and [0, 3], a wall fixed on [2, 1], [1, 0] kept plain, the end [3, 3]
    private static int?[,] Regions()
    {
        var regions = new int?[4, 4];
        regions[0, 2] = 1;
        regions[0, 3] = 1;
        return regions;
    }

    private static Tile?[,] Tiles()
    {
        var tiles = new Tile?[4, 4];
        tiles[2, 1] = Tile.Wall;
        tiles[1, 0] = Tile.None;
        return tiles;
    }

    private static readonly Cell End = new(3, 3);

    private static List<Cell> Starts() => [new(0, 0), new(1, 0)];

    private static MapSkeleton Valid() => new(Regions(), Tiles(), End, Starts());

    [Fact]
    public void Valid_skeleton_reads_back_its_squares()
    {
        var skeleton = Valid();

        Assert.Equal(
            (4, (int?)1, (int?)null, (Tile?)Tile.Wall, (Tile?)Tile.None, (Tile?)null, End),
            (skeleton.Size, skeleton.RegionAt(new(0, 2)), skeleton.RegionAt(new(1, 1)), skeleton.TileAt(new(2, 1)), skeleton.TileAt(new(1, 0)), skeleton.TileAt(new(1, 1)), skeleton.End));
    }

    [Fact]
    public void Valid_skeleton_keeps_its_starts()
    {
        Assert.Equal(Starts(), Valid().Starts);
    }

    [Fact]
    public void Changing_the_callers_arrays_changes_nothing()
    {
        var regions = Regions();
        var tiles = Tiles();
        var starts = Starts();
        var skeleton = new MapSkeleton(regions, tiles, End, starts);

        regions[0, 2] = 5;
        tiles[2, 1] = null;
        starts.Clear();

        Assert.Equal(((int?)1, (Tile?)Tile.Wall, 2), (skeleton.RegionAt(new(0, 2)), skeleton.TileAt(new(2, 1)), skeleton.Starts.Count));
    }

    public static TheoryData<string> InvalidCases => new(Invalid.Keys);

    private static readonly Dictionary<string, Func<MapSkeleton>> Invalid = new()
    {
        ["layers of different sizes"] = () => new(Regions(), new Tile?[3, 3], new(2, 2), [new(0, 0)]),
        ["not square"] = () => new(new int?[4, 3], new Tile?[4, 3], new(3, 2), [new(0, 0)]),
        ["too small"] = () => new(new int?[1, 1], new Tile?[1, 1], new(0, 0), []),
        ["end off the map"] = () => new(Regions(), Tiles(), new(4, 0), Starts()),
        ["fixed region on the end"] = () =>
        {
            var regions = Regions();
            regions[3, 3] = 1;
            return new(regions, Tiles(), End, Starts());
        },
        ["wall on the end"] = () =>
        {
            var tiles = Tiles();
            tiles[3, 3] = Tile.Wall;
            return new(Regions(), tiles, End, Starts());
        },
        ["wall on a start square"] = () =>
        {
            var tiles = Tiles();
            tiles[0, 0] = Tile.Wall;
            return new(Regions(), tiles, End, Starts());
        },
        ["start off the map"] = () => new(Regions(), Tiles(), End, [new(5, 5)]),
        ["start on the end"] = () => new(Regions(), Tiles(), End, [End]),
        ["start repeated"] = () => new(Regions(), Tiles(), End, [new(0, 0), new(0, 0)]),
        ["start right of the end"] = () => new(Regions(), Tiles(), new(3, 2), [new(0, 3)]),
        ["fixed region in two pieces"] = () =>
        {
            var regions = Regions();
            regions[0, 0] = 2;
            regions[2, 2] = 2;
            return new(regions, Tiles(), End, [new(1, 0)]);
        },
    };

    [Theory]
    [MemberData(nameof(InvalidCases))]
    public void Invalid_skeleton_throws(string name)
    {
        Assert.Throws<ArgumentException>(Invalid[name]);
    }
}
