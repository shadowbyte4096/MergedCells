namespace MergedCells.Core;

// Trapped: a trap sent the player back to the start, so Stop is the start.
// Teleported: the slide went through at least one teleporter, so it counts even when it ends where it began.
// Looped: the slide would never stop, so it doesn't happen: Stop is where it began, and the other flags are false.
public readonly record struct SlideResult(Cell Stop, bool ReachedEnd, bool Trapped = false, bool Teleported = false, bool Looped = false);
