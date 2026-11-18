namespace MergedCells.Core;

/// <summary>How a visit to a map ended: won, given up with New map, or left (the map replaced or the screen gone).</summary>
public enum PlayOutcome
{
    Won,
    GaveUp,
    Left,
}

public enum PlayActionKind
{
    Up,
    Down,
    Left,
    Right,
    Undo,
    Restart,
}

/// <summary>A counted move, an undo that took a move back, or a restart, at <paramref name="At"/> milliseconds of active time.</summary>
public sealed record PlayAction(long At, PlayActionKind Kind);

/// <summary>
/// Everything known about a visit to a map when it starts. <paramref name="Level"/> and <paramref name="SeenBefore"/> are
/// a campaign level's, <paramref name="Infinite"/> an infinite map's. The metrics are as counted then:
/// <paramref name="MetricsVersion"/> and the two config numbers that change counts say how.
/// </summary>
public sealed record PlayRecordStart(
    Guid Id,
    Guid Player,
    DateTimeOffset Started,
    string Build,
    string Platform,
    DifficultyMode Mode,
    int? Level,
    bool? SeenBefore,
    InfiniteChoice? Infinite,
    Map Map,
    int Par,
    int MetricsVersion,
    int NearOptimalExtraMoves,
    int BruteForceLookahead,
    DifficultyMetrics Metrics,
    int Score);

/// <summary>One visit to a map, for refitting the difficulty weights: the map as it started and how it was played.</summary>
public sealed record PlayRecord(
    PlayRecordStart Start,
    PlayOutcome Outcome,
    int Moves,
    int Undos,
    int Restarts,
    long ActiveMs,
    IReadOnlyList<PlayAction> Actions);
