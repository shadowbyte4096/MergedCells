namespace MergedCells.Core;

// How often a route stops against a wall: a slide that stops on a square because the next one is a wall, where it
// would otherwise have slid on. A slide that entered its last square across a region boundary stopped there anyway,
// and a win, a trap or a teleporter ends a slide for its own reason, so none of those count. The walls chapter's
// rule that about half its levels use walls as stoppers is measured with it
internal static class WallStops
{
    public static int Count(Map map, IReadOnlyList<Direction> path)
    {
        var count = 0;
        var at = map.Start;
        foreach (var direction in path)
        {
            var slide = map.Slide(at, direction);
            var stop = slide.Stop;
            if (!slide.ReachedEnd && !slide.Trapped && !slide.Teleported && stop != at)
            {
                var next = stop.Step(direction);
                var before = Back(stop, direction);
                if (map.Contains(next) && map.TileAt(next) == Tile.Wall && map.RegionAt(before) == map.RegionAt(stop))
                {
                    count++;
                }
            }
            at = stop;
        }
        return count;
    }

    private static Cell Back(Cell cell, Direction direction) => direction switch
    {
        Direction.Up => cell.Step(Direction.Down),
        Direction.Down => cell.Step(Direction.Up),
        Direction.Left => cell.Step(Direction.Right),
        _ => cell.Step(Direction.Left),
    };
}
