namespace MergedCells.Core;

// A slide for animation: its result, and the square the straight slide reached before anything sent the player elsewhere
// (the trap that caught them, otherwise the stop)
public readonly record struct SlideTrace(SlideResult Result, Cell Reached);
