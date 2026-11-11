namespace MergedCells.Core;

// A slide for animation: its result, and its straight runs in order. The last run ends where the player stopped, or on
// the trap that sent them back to the start. A no-op, a loop included, is one run from the square back to itself
public readonly record struct SlideTrace(SlideResult Result, IReadOnlyList<SlideRun> Runs);
