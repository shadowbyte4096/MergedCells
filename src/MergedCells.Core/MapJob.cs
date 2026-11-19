namespace MergedCells.Core;

/// <summary>
/// One infinite map being made on a thread-pool thread, so a big map never blocks the game. The game polls it each frame:
/// <see cref="Progress"/> while it runs, then <see cref="Map"/>, <see cref="Failure"/> or <see cref="IsCancelled"/> once
/// <see cref="IsDone"/>. It reads nothing global, and nothing it throws escapes its thread.
/// </summary>
public sealed class MapJob
{
    private readonly CancellationTokenSource cancel = new();
    private readonly int steps;
    private int stepsTaken;

    // The work starts at once. It's given the job's token and a counter for the steps it has used, out of steps
    internal MapJob(Func<CancellationToken, Action<int>, InfiniteMap?> work, int steps)
    {
        ArgumentNullException.ThrowIfNull(work);
        ArgumentOutOfRangeException.ThrowIfLessThan(steps, 1);
        this.steps = steps;
        var token = cancel.Token;
        Finished = Task.Run(() =>
        {
            try
            {
                Map = work(token, count => Volatile.Write(ref stepsTaken, count));
            }
            catch (OperationCanceledException)
            {
                IsCancelled = true;
            }
            catch (Exception exception)
            {
                Failure = exception;
            }
        });
    }

    /// <summary>Starts making the map for the choice and seed, as <see cref="InfiniteMaps.Next"/> would.</summary>
    public static MapJob Start(InfiniteMaps maps, InfiniteChoice choice, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(maps);
        ArgumentNullException.ThrowIfNull(choice);
        return new MapJob((token, count) => maps.Next(choice, seed, token, count), maps.Steps);
    }

    /// <summary>The climb steps used so far.</summary>
    public int StepsTaken => Volatile.Read(ref stepsTaken);

    /// <summary>The steps used out of the budget, from 0 to 1.</summary>
    public double Progress => Math.Min(1.0, StepsTaken / (double)steps);

    /// <summary>Whether the work has returned, thrown, or stopped after a cancel.</summary>
    public bool IsDone => Finished.IsCompleted;

    /// <summary>Once done: the map, or null when none was made, the work threw, or it was cancelled.</summary>
    public InfiniteMap? Map { get; private set; }

    /// <summary>Once done: what the work threw, other than stopping for a cancel.</summary>
    public Exception? Failure { get; private set; }

    /// <summary>Once done: whether the work stopped because of <see cref="Cancel"/>.</summary>
    public bool IsCancelled { get; private set; }

    // Completes when the work ends, and never faults
    internal Task Finished { get; }

    /// <summary>Asks the work to stop. Safe at any time and more than once; once the work has finished it changes nothing.</summary>
    public void Cancel() => cancel.Cancel();
}
