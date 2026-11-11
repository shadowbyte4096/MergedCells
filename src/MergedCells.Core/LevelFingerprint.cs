namespace MergedCells.Core;

/// <summary>
/// A fingerprint of everything that changes how a level plays: the grid's size, the regions' shapes, the start, the
/// end, the par, any twist tiles and which squares teleporters link. Saved bests carry it, so a best from a level that has since changed is ignored. A
/// level without twists feeds nothing for them, so adding twist support never changes a plain level's fingerprint.
/// </summary>
public static class LevelFingerprint
{
    // 64-bit FNV-1a. Never change the algorithm or the order things are fed in: every saved best would stop matching
    private const ulong OffsetBasis = 0xcbf29ce484222325;
    private const ulong Prime = 0x100000001b3;
    private const int TilesSection = 1;
    private const int TeleportersSection = 2;

    public static string Of(Level level)
    {
        ArgumentNullException.ThrowIfNull(level);
        return Of(level.Map, level.Par);
    }

    /// <summary>16 lowercase hexadecimal digits. Region IDs are relabelled in reading order first, so renumbering
    /// regions without changing their shapes keeps the fingerprint.</summary>
    public static string Of(Map map, int par)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentOutOfRangeException.ThrowIfLessThan(par, 1);

        var hash = OffsetBasis;
        Feed(ref hash, map.Rows);
        Feed(ref hash, map.Columns);
        var labels = new Dictionary<int, int>();
        for (var row = 0; row < map.Rows; row++)
        {
            for (var column = 0; column < map.Columns; column++)
            {
                var region = map.RegionAt(new Cell(row, column));
                if (!labels.TryGetValue(region, out var label))
                {
                    label = labels.Count;
                    labels.Add(region, label);
                }
                Feed(ref hash, label);
            }
        }
        Feed(ref hash, map.Start.Row);
        Feed(ref hash, map.Start.Column);
        Feed(ref hash, map.End.Row);
        Feed(ref hash, map.End.Column);
        Feed(ref hash, par);
        if (map.HasTiles)
        {
            // A marker before each twist section keeps one section's numbers from reading as another's
            Feed(ref hash, TilesSection);
            for (var row = 0; row < map.Rows; row++)
            {
                for (var column = 0; column < map.Columns; column++)
                {
                    Feed(ref hash, (int)map.TileAt(new Cell(row, column)));
                }
            }
        }
        if (map.Teleporters.Count > 0)
        {
            // Per square, so neither the pairs' order (only their letters) nor a pair's own order changes the fingerprint
            Feed(ref hash, TeleportersSection);
            foreach (var partner in Partners(map))
            {
                Feed(ref hash, partner);
            }
        }
        return hash.ToString("x16");
    }

    // Each square's partner as row * columns + column, or -1, in reading order
    private static int[] Partners(Map map)
    {
        var partners = new int[map.Rows * map.Columns];
        Array.Fill(partners, -1);
        foreach (var pair in map.Teleporters)
        {
            partners[pair.First.Row * map.Columns + pair.First.Column] = pair.Second.Row * map.Columns + pair.Second.Column;
            partners[pair.Second.Row * map.Columns + pair.Second.Column] = pair.First.Row * map.Columns + pair.First.Column;
        }
        return partners;
    }

    // Least significant byte first, whatever the machine's byte order
    private static void Feed(ref ulong hash, int value)
    {
        var bits = unchecked((uint)value);
        for (var shift = 0; shift < 32; shift += 8)
        {
            hash ^= (bits >> shift) & 0xff;
            hash = unchecked(hash * Prime);
        }
    }
}
