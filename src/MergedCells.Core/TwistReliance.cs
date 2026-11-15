namespace MergedCells.Core;

/// <summary>
/// How much a map's twists matter: how far par moves, either way, when every wall, sticky square, trap and teleporter
/// is taken off and the regions, start and end stay. A twist chapter's levels should rely on their twists.
/// </summary>
public static class TwistReliance
{
    /// <summary>
    /// The size of the change in par without the twists: 0 when they don't change it, or when the map has none. Null
    /// when the map can't be won without them. Throws <see cref="ArgumentException"/> when the map itself can't be won.
    /// </summary>
    public static int? Of(Map map)
    {
        ArgumentNullException.ThrowIfNull(map);
        var par = Solver.Solve(map).Par ?? throw new ArgumentException("The map can't be won.", nameof(map));
        return Of(map, par);
    }

    // For the climber, which has already solved the map
    internal static int? Of(Map map, int par)
    {
        if (!map.HasTiles && map.Teleporters.Count == 0)
        {
            return 0;
        }
        return Solver.Solve(WithoutTwists(map)).Par is { } plainPar ? Math.Abs(par - plainPar) : null;
    }

    internal static Map WithoutTwists(Map map) => Map.Create(map.RegionGrid(), map.Start, map.End);
}
