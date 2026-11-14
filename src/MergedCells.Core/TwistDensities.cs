namespace MergedCells.Core;

/// <summary>
/// How much of each twist <see cref="MapGenerator.Generate"/> puts on a map: each a share of the map's squares, in
/// percent from 0 to 100, rounded up. 0 leaves that twist off. Teleporters are that share of squares made into pairs, at
/// most <see cref="TeleporterLetters.Count"/> of them.
/// </summary>
public sealed record TwistDensities(int WallPercent = 0, int StickyPercent = 0, int TrapPercent = 0, int TeleporterPercent = 0);
