namespace MergedCells.Core;

/// <summary>A generated map and the solver's result for it, which is solvable with par in the requested range.</summary>
public sealed record GeneratedMap(Map Map, SolveResult Solution);
