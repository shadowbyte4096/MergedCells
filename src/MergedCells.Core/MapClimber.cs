namespace MergedCells.Core;

/// <summary>
/// Pushes generated maps toward a target difficulty: from one of <see cref="MapGenerator"/>'s maps it tries one small
/// edit per step (split a region, merge two, move a square between regions, move the start, move a twist) and keeps it
/// when the map gets no further from the target, starting again from a fresh map when it stalls. Every kept map passes
/// the generator's filter and keeps its shapes, start area and twist counts. One seed, request and config give one map
/// on every device. Immutable, so one climber can serve several threads.
/// </summary>
public sealed class MapClimber
{
    // A climb's work grows with this; ten million steps is hours even on small maps
    private const int MostSteps = 10_000_000;

    private readonly GeneratorConfig generatorConfig;
    private readonly MapGenerator generator;
    private readonly DifficultyRater rater;
    private readonly DifficultyCaps caps;
    private readonly ClimbConfig climb;
    // Indexed by ClimbEdit
    private readonly int[] weights;

    /// <summary>Throws <see cref="ArgumentOutOfRangeException"/> naming any number out of range in the three groups.</summary>
    public MapClimber(GeneratorConfig generator, DifficultyConfig difficulty, ClimbConfig climb)
    {
        this.generator = new MapGenerator(generator);
        rater = new DifficultyRater(difficulty);
        ArgumentNullException.ThrowIfNull(climb);
        ArgumentOutOfRangeException.ThrowIfLessThan(climb.MaxSteps, 1, "climb.maxSteps");
        ArgumentOutOfRangeException.ThrowIfGreaterThan(climb.MaxSteps, MostSteps, "climb.maxSteps");
        ArgumentOutOfRangeException.ThrowIfLessThan(climb.StallSteps, 1, "climb.stallSteps");
        ArgumentOutOfRangeException.ThrowIfGreaterThan(climb.StallSteps, climb.MaxSteps, "climb.stallSteps");
        ArgumentOutOfRangeException.ThrowIfNegative(climb.SplitWeight, "climb.splitWeight");
        ArgumentOutOfRangeException.ThrowIfNegative(climb.MergeWeight, "climb.mergeWeight");
        ArgumentOutOfRangeException.ThrowIfNegative(climb.MoveSquareWeight, "climb.moveSquareWeight");
        ArgumentOutOfRangeException.ThrowIfNegative(climb.MoveStartWeight, "climb.moveStartWeight");
        ArgumentOutOfRangeException.ThrowIfNegative(climb.MoveTwistWeight, "climb.moveTwistWeight");
        weights = [climb.SplitWeight, climb.MergeWeight, climb.MoveSquareWeight, climb.MoveStartWeight, climb.MoveTwistWeight];
        if (weights.Sum(weight => (long)weight) is < 1 or > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(climb), "The climb's five edit weights must add up to at least 1 and fit in a whole number.");
        }
        generatorConfig = generator;
        caps = difficulty.Caps;
        this.climb = climb;
    }

    /// <summary>
    /// Climbs for up to the config's <c>maxSteps</c> steps from the random stream started at <paramref name="seed"/>, and
    /// returns the first map that hits the target, or the nearest found (the earliest of equals). Null only when the
    /// generator never gave a starting map within the steps. Throws <see cref="ArgumentOutOfRangeException"/> for a
    /// target or request out of range or an undefined mode, and <see cref="ArgumentException"/> when the only edits with
    /// a weight move twists and the request has none.
    /// </summary>
    public ClimbedMap? Climb(ClimbRequest request, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Map);
        TargetDistance.Check(request.Target);
        if (!Enum.IsDefined(request.Mode))
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.Mode, "Not a difficulty mode.");
        }
        var available = (int[])weights.Clone();
        if (request.Map.Twists is not { } twists
            || (long)twists.WallPercent + twists.StickyPercent + twists.TrapPercent + twists.TeleporterPercent == 0)
        {
            available[(int)ClimbEdit.MoveTwist] = 0;
        }
        var total = available.Sum();
        if (total == 0)
        {
            throw new ArgumentException("Only twist moves have a weight, and the request has no twists.", nameof(request));
        }

        var random = new GameRandom(seed);
        Evaluated? best = null;
        var steps = 0;
        while (steps < climb.MaxSteps)
        {
            var start = generator.Generate(request.Map, ((ulong)random.NextUInt() << 32) | random.NextUInt());
            var current = start is null ? null : Evaluate(ClimbMap.From(start.Map), request);
            if (current is null)
            {
                steps += climb.StallSteps;
                continue;
            }
            steps++;
            best = Nearer(best, current);
            var startArea = StartAndEnd.Starts(current.State.Size, current.State.End, generatorConfig);
            var climbBest = current.Distance;
            var stall = 0;
            while (current.Distance > 0 && steps < climb.MaxSteps && stall < climb.StallSteps)
            {
                steps++;
                stall++;
                var edited = current.State.Copy();
                if (!ClimbEdits.Apply(Pick(available, total, random), edited, random, startArea))
                {
                    continue;
                }
                if (Evaluate(edited, request) is not { } next)
                {
                    continue;
                }
                if (next.Distance <= current.Distance)
                {
                    current = next;
                }
                if (next.Distance < climbBest)
                {
                    climbBest = next.Distance;
                    stall = 0;
                }
                best = Nearer(best, current);
            }
            if (best.Distance == 0)
            {
                break;
            }
        }
        return best is null
            ? null
            : new ClimbedMap(best.Map, best.Solution, best.Metrics, best.Score, best.TwistReliance, best.Distance);
    }

    // Null when the map breaks a shape or the generator's filter; otherwise everything measured from one move graph
    private Evaluated? Evaluate(ClimbMap state, ClimbRequest request)
    {
        if (!MapShapes.Fits(state.Regions, state.End, generatorConfig, request.Map.Bends))
        {
            return null;
        }
        var map = state.ToMap();
        var graph = MoveGraph.Build(map.Start, map.Slide);
        var solution = Solver.SolveGraph(graph);
        if (!MapGenerator.Keeps(solution, request.Map))
        {
            return null;
        }
        var metrics = rater.Measure(graph, map.End)!;
        var score = rater.Score(metrics, request.Mode);
        int? reliance = request.Target.MinTwistReliance > 0 ? TwistReliance.Of(map, metrics, rater) : null;
        var distance = TargetDistance.Of(request.Target, metrics, score, reliance, caps);
        return new Evaluated(state, map, solution, metrics, score, reliance, distance);
    }

    // Only a strictly nearer map replaces the best, so ties keep the earliest
    private static Evaluated Nearer(Evaluated? best, Evaluated candidate) =>
        best is null || candidate.Distance < best.Distance ? candidate : best;

    private static ClimbEdit Pick(int[] available, int total, GameRandom random)
    {
        var roll = random.Next(total);
        for (var edit = 0; edit < available.Length; edit++)
        {
            if (roll < available[edit])
            {
                return (ClimbEdit)edit;
            }
            roll -= available[edit];
        }
        throw new InvalidOperationException("The roll is always below the weights' total.");
    }

    private sealed record Evaluated(
        ClimbMap State, Map Map, SolveResult Solution, DifficultyMetrics Metrics, int Score, int? TwistReliance, int Distance);
}
