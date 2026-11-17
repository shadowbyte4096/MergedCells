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

    // Two pairs on free squares of the 4x4 skeleton
    private static List<TeleporterPair> Pairs() => [new(new(1, 1), new(2, 3)), new(new(1, 2), new(3, 0))];

    private static MapSkeleton WithPairs(IReadOnlyList<TeleporterPair> pairs, Tile?[,]? tiles = null) =>
        new(Regions(), tiles ?? Tiles(), End, Starts(), pairs);

    // 10x10 with no starts and the end in the far corner: squares paired with the next in reading order, from the top-left
    private static MapSkeleton PairsOnTen(int count)
    {
        var pairs = Enumerable.Range(0, count).Select(i => new TeleporterPair(new(2 * i / 10, 2 * i % 10), new((2 * i + 1) / 10, (2 * i + 1) % 10))).ToList();
        return new(new int?[10, 10], new Tile?[10, 10], new(9, 9), [], pairs);
    }

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

    [Fact]
    public void Valid_skeleton_keeps_its_pairs_in_order()
    {
        Assert.Equal(Pairs(), WithPairs(Pairs()).Teleporters);
    }

    [Fact]
    public void Skeleton_without_pairs_has_none()
    {
        Assert.Empty(Valid().Teleporters);
    }

    [Fact]
    public void Pair_squares_read_as_fixed_plain_squares()
    {
        var skeleton = WithPairs(Pairs());

        Assert.All(Pairs().SelectMany(pair => new[] { pair.First, pair.Second }), cell => Assert.Equal(Tile.None, skeleton.TileAt(cell)));
    }

    [Fact]
    public void Pair_errors_name_the_teleporters_parameter()
    {
        var problem = Assert.Throws<ArgumentException>(() => WithPairs([new(new(1, 1), new(1, 1))]));

        Assert.Equal("teleporters", problem.ParamName);
    }

    [Fact]
    public void Skeleton_takes_one_pair_per_letter()
    {
        Assert.Equal(TeleporterLetters.Count, PairsOnTen(TeleporterLetters.Count).Teleporters.Count);
    }

    [Fact]
    public void Changing_the_callers_pair_list_changes_nothing()
    {
        var pairs = Pairs();
        var skeleton = WithPairs(pairs);

        pairs.Clear();

        Assert.Equal(2, skeleton.Teleporters.Count);
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
        ["pair off the map"] = () => WithPairs([new(new(1, 1), new(4, 0))]),
        ["pair on one square"] = () => WithPairs([new(new(1, 1), new(1, 1))]),
        ["square in two pairs"] = () => WithPairs([new(new(1, 1), new(2, 3)), new(new(1, 2), new(1, 1))]),
        ["pair on the end"] = () => WithPairs([new(new(1, 1), End)]),
        ["pair on a start square"] = () => WithPairs([new(new(0, 0), new(1, 1))]),
        ["pair on a fixed wall"] = () => WithPairs([new(new(2, 1), new(1, 1))]),
        ["pair on a fixed trap"] = () =>
        {
            var tiles = Tiles();
            tiles[1, 1] = Tile.Trap;
            return WithPairs([new(new(1, 1), new(2, 3))], tiles);
        },
        ["more pairs than letters"] = () => PairsOnTen(TeleporterLetters.Count + 1),
    };

    [Theory]
    [MemberData(nameof(InvalidCases))]
    public void Invalid_skeleton_throws(string name)
    {
        Assert.Throws<ArgumentException>(Invalid[name]);
    }
}
