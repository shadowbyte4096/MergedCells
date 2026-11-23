namespace MergedCells.Core;

/// <summary>
/// One finish of an infinite map. <see cref="Efficiency"/> is this finish's; <see cref="KeptMoves"/> and
/// <see cref="KeptPoints"/> are the map's counted finish after it, which this one replaced only when <see cref="Counted"/>.
/// <see cref="PreviousMoves"/> is the map's counted finish before it, null on its first.
/// </summary>
public sealed record MapScore(
    int Par,
    int Moves,
    int Efficiency,
    int StreakLimit,
    int StreakBefore,
    int? PreviousMoves,
    bool Counted,
    int KeptMoves,
    long KeptPoints)
{
    public bool OverLimit => Moves > StreakLimit;
}

/// <summary>
/// One infinite run: every map from Play on the setup screen until play is left. Each map counts its best finish, so a Retry
/// can only improve it, and is locked in when the next map starts. A give-up scores nothing, counts as 0% efficiency and
/// breaks the streak. Main thread only.
/// </summary>
public sealed class InfiniteRun
{
    private readonly InfiniteScoring scoring;
    private readonly StarsConfig stars;
    private readonly List<int> efficiencies = new();
    private long committedScore;
    private int streakBefore;
    private int? par;
    private Outcome? current;

    public InfiniteRun(InfiniteScoring scoring, StarsConfig stars)
    {
        ArgumentNullException.ThrowIfNull(scoring);
        ArgumentNullException.ThrowIfNull(stars);
        this.scoring = scoring;
        this.stars = stars;
    }

    public long Score => committedScore + (current?.Points ?? 0);

    // After the current map's outcome, or before it while it has none
    public int Streak => current?.StreakAfter ?? streakBefore;

    // Maps with an outcome, the current one included
    public int Maps => efficiencies.Count + (current is null ? 0 : 1);

    /// <summary>The average efficiency of the last window's maps, rounded down, or null until the run has that many.</summary>
    public int? WindowAverage
    {
        get
        {
            var window = scoring.EfficiencyWindow;
            return Maps < window ? null : (int)(Efficiencies().TakeLast(window).Sum(value => (long)value) / window);
        }
    }

    // Every map's so far, for before the window is full
    public int RunningAverage => Maps == 0 ? 0 : (int)(Efficiencies().Sum(value => (long)value) / Maps);

    public InfiniteBest Best => new(Streak, Score, WindowAverage);

    /// <summary>Locks in the current map (one with no outcome counts as given up) and opens the next.</summary>
    public void StartMap(int par)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(par, 1);
        if (this.par is not null)
        {
            var outcome = current ?? Outcome.GivenUp;
            committedScore += outcome.Points;
            efficiencies.Add(outcome.Efficiency);
            streakBefore = outcome.StreakAfter;
        }
        this.par = par;
        current = null;
    }

    /// <summary>
    /// A finish of the current map, kept as its result when it's the map's first or takes fewer moves than its best. Throws
    /// <see cref="ArgumentOutOfRangeException"/> when moves are below par, which only a wrong par can give.
    /// </summary>
    public MapScore Finish(int moves)
    {
        var mapPar = par ?? throw new InvalidOperationException("No map has started.");
        var efficiency = scoring.Efficiency(mapPar, moves);
        var limit = Stars.TwoStarLimit(mapPar, stars);
        var previous = current?.Moves;
        var counted = current is not { Moves: { } best } || moves < best;
        if (counted)
        {
            current = new Outcome(moves, scoring.Points(mapPar, moves, streakBefore), efficiency, moves <= limit ? streakBefore + 1 : 0);
        }
        return new MapScore(mapPar, moves, efficiency, limit, streakBefore, previous, counted, current!.Moves!.Value, current.Points);
    }

    /// <summary>Gives the current map up, unless it already has a finish: a solved map keeps its result.</summary>
    public void GiveUp()
    {
        if (par is null)
        {
            throw new InvalidOperationException("No map has started.");
        }
        current ??= Outcome.GivenUp;
    }

    private IEnumerable<int> Efficiencies() => current is null ? efficiencies : efficiencies.Append(current.Efficiency);

    // Moves is null for a give-up
    private sealed record Outcome(int? Moves, long Points, int Efficiency, int StreakAfter)
    {
        public static Outcome GivenUp { get; } = new(null, 0, 0, 0);
    }
}
