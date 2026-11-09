namespace MergedCells.Core;

public sealed record Level(int Id, string Name, int Chapter, Map Map, int Par, Tutorial? Tutorial = null);
