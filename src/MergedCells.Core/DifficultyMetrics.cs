namespace MergedCells.Core;

/// <summary>
/// What <see cref="DifficultyRater.Measure"/> counts on a map. Every move is measured by where it lands, by rows plus
/// columns to the end. Plain counts, so a stored set can be scored again when the weights change.
/// </summary>
/// <param name="Par">The fewest moves that win.</param>
/// <param name="AwayMoves">The fewest moves away from the end on any optimal route.</param>
/// <param name="Retreat">The least distance any optimal route gives up, summed over its moves away from the end.</param>
/// <param name="GreedyGap">
/// The fewest moves on a winning route that never moves away from the end, minus par; null when every winning route does.
/// </param>
/// <param name="Decoys">
/// The fewest decoys along any optimal route: moves that aren't optimal but land no further from the end than the route's
/// own move from the same square.
/// </param>
/// <param name="NearOptimalRoutes">
/// The move sequences that win in par + 1 up to par + the config's <c>nearOptimalExtraMoves</c>, capped at
/// <see cref="long.MaxValue"/>.
/// </param>
/// <param name="BruteForceExtraMoves">
/// The fewest moves any of the config's lookahead players takes to win, minus par; null when none finishes.
/// </param>
public sealed record DifficultyMetrics(
    int Par,
    int AwayMoves,
    int Retreat,
    int? GreedyGap,
    int Decoys,
    long NearOptimalRoutes,
    int? BruteForceExtraMoves);
