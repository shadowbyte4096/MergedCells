namespace MergedCells.Core;

// Number counts from 1, in the chapter file's order; Levels are in play order
public sealed record Chapter(int Number, string Name, IReadOnlyList<Level> Levels);
