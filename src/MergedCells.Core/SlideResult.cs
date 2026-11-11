namespace MergedCells.Core;

// Trapped: a trap sent the player back to the start, so Stop is the start
public readonly record struct SlideResult(Cell Stop, bool ReachedEnd, bool Trapped = false);
