namespace MergedCells.Core;

// A map the climber can edit in place: its grids, pairs and start, with region IDs kept 1 to RegionCount in reading
// order of each region's first square, so per-region data fits in arrays and no order depends on a dictionary
internal sealed class ClimbMap
{
    private ClimbMap(int[,] regions, Tile[,] tiles, List<TeleporterPair> teleporters, Cell start, Cell end, int regionCount)
    {
        Regions = regions;
        Tiles = tiles;
        Teleporters = teleporters;
        Start = start;
        End = end;
        RegionCount = regionCount;
    }

    public int Size => Regions.GetLength(0);

    public int[,] Regions { get; }

    public int RegionCount { get; private set; }

    public Tile[,] Tiles { get; }

    // In the map's order, which gives each pair its letter
    public List<TeleporterPair> Teleporters { get; }

    public Cell Start { get; set; }

    public Cell End { get; }

    public static ClimbMap From(Map map)
    {
        var climbMap = new ClimbMap(map.RegionGrid(), map.TileGrid(), [.. map.Teleporters], map.Start, map.End, 0);
        climbMap.Renumber();
        return climbMap;
    }

    public ClimbMap Copy() =>
        new((int[,])Regions.Clone(), (Tile[,])Tiles.Clone(), [.. Teleporters], Start, End, RegionCount);

    public Map ToMap() => Map.Create(Regions, Start, End, Tiles, Teleporters);

    public bool Contains(Cell cell) => Map.IsInside(cell, Size, Size);

    public int RegionAt(Cell cell) => Regions[cell.Row, cell.Column];

    // Where a twist may go: on the map, not the start or the end, and holding no twist or teleporter
    public bool IsFree(Cell cell) =>
        Contains(cell) && cell != Start && cell != End && Tiles[cell.Row, cell.Column] == Tile.None && !IsTeleporter(cell);

    public bool IsTeleporter(Cell cell)
    {
        foreach (var pair in Teleporters)
        {
            if (pair.First == cell || pair.Second == cell)
            {
                return true;
            }
        }
        return false;
    }

    public IEnumerable<Cell> Cells()
    {
        for (var row = 0; row < Size; row++)
        {
            for (var column = 0; column < Size; column++)
            {
                yield return new Cell(row, column);
            }
        }
    }

    // A region edit can leave gaps in the IDs or use new ones; this numbers them 1 to RegionCount again
    public void Renumber()
    {
        var numbers = new Dictionary<int, int>();
        for (var row = 0; row < Size; row++)
        {
            for (var column = 0; column < Size; column++)
            {
                if (!numbers.TryGetValue(Regions[row, column], out var number))
                {
                    number = numbers.Count + 1;
                    numbers[Regions[row, column]] = number;
                }
                Regions[row, column] = number;
            }
        }
        RegionCount = numbers.Count;
    }
}
