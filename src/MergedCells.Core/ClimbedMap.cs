namespace MergedCells.Core;

/// <summary>
/// The nearest map a climb found, with its solution, metrics, score and twist reliance. <paramref name="Distance"/> is
/// how far it is from the target in score points: 0 means it hits it.
/// </summary>
public sealed record ClimbedMap(Map Map, SolveResult Solution, DifficultyMetrics Metrics, int Score, int? TwistReliance, int Distance);
