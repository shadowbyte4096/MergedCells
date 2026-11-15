namespace MergedCells.Core;

/// <summary>
/// How much a map's twists matter. A twist earns its place when taking its kind off, with everything else staying,
/// changes what the map feels like: par, the decoys on the best route, the moves a brute-force player wastes, or the
/// moves away from the end. A tempting twist that turns out to be a red herring counts, though par never moves.
/// Walls, sticky squares, traps and teleporters are each taken off in turn, and the least of their changes is the
/// reliance, so every kind on the map has to matter. A twist chapter's levels should rely on their twists.
/// </summary>
public static class TwistReliance
{
    /// <summary>
    /// The most the reliance can be: par, decoys, brute-force excess and away moves each count one when they change.
    /// </summary>
    public const int Most = 4;

    /// <summary>
    /// How many of par, decoys, brute-force excess and away moves change without each kind of twist, the least over the
    /// kinds on the map: 0 when some kind changes none of them, or when the map has no twists. A kind whose removal
    /// leaves a map that can't be won counts as <see cref="Most"/>. Throws <see cref="ArgumentException"/> when the map
    /// itself can't be won.
    /// </summary>
    public static int Of(Map map, DifficultyRater rater)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(rater);
        var metrics = rater.Measure(map) ?? throw new ArgumentException("The map can't be won.", nameof(map));
        return Of(map, metrics, rater);
    }

    // For the climber, which has already measured the map
    internal static int Of(Map map, DifficultyMetrics metrics, DifficultyRater rater)
    {
        var tiles = map.TileGrid();
        var without = new List<Map>();
        foreach (var tile in new[] { Tile.Wall, Tile.Sticky, Tile.Trap })
        {
            if (Contains(tiles, tile))
            {
                without.Add(WithoutTile(map, tile));
            }
        }
        if (map.Teleporters.Count > 0)
        {
            without.Add(Map.Create(map.RegionGrid(), map.Start, map.End, map.HasTiles ? map.TileGrid() : null));
        }
        if (without.Count == 0)
        {
            return 0;
        }
        return without.Min(plain => rater.Measure(plain) is { } other ? Changes(metrics, other) : Most);
    }

    internal static int Changes(DifficultyMetrics a, DifficultyMetrics b) =>
        (a.Par != b.Par ? 1 : 0)
        + (a.Decoys != b.Decoys ? 1 : 0)
        + (a.BruteForceExtraMoves != b.BruteForceExtraMoves ? 1 : 0)
        + (a.AwayMoves != b.AwayMoves ? 1 : 0);

    private static bool Contains(Tile[,] tiles, Tile tile)
    {
        foreach (var square in tiles)
        {
            if (square == tile)
            {
                return true;
            }
        }
        return false;
    }

    // The map with every square of one tile kind made plain; the other tiles and the teleporters stay
    private static Map WithoutTile(Map map, Tile tile)
    {
        var tiles = map.TileGrid();
        for (var row = 0; row < map.Rows; row++)
        {
            for (var column = 0; column < map.Columns; column++)
            {
                if (tiles[row, column] == tile)
                {
                    tiles[row, column] = Tile.None;
                }
            }
        }
        return Map.Create(map.RegionGrid(), map.Start, map.End, tiles, map.Teleporters);
    }

    internal static Map WithoutTwists(Map map) => Map.Create(map.RegionGrid(), map.Start, map.End);
}
