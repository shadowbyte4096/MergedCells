namespace MergedCells.Core;

// Entering either square moves the player to the other, so the order doesn't matter to the rule
public readonly record struct TeleporterPair(Cell First, Cell Second);
