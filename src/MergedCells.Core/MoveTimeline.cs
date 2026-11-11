namespace MergedCells.Core;

// Where the player is drawn: Progress of the way from From to To (the same square when it isn't sliding), at Scale (0 is
// hidden). Sliding is true only while it moves along a run, when its trail grows
public readonly record struct MoveFrame(Cell From, Cell To, double Progress, double Scale, bool Sliding);

public enum MoveEventKind
{
    // The player has slid into teleporter Index (the jump's number)
    ReachTeleporter,

    // The player has grown out of a partner, and run Index starts
    BeginRun,

    // A trap has caught the player at the end of the last run
    Catch,
}

// Something the view does once the move's clock reaches At
public readonly record struct MoveEvent(MoveEventKind Kind, int Index, double At);

/// <summary>
/// One counted move as it plays out over time: its straight runs at slide speed, a blink at each teleporter between
/// them, and the trap's return after the last when one caught the player. Immutable; <see cref="WithWait"/> gives a copy
/// once the camera says how long a jump must wait for it.
/// </summary>
public sealed class MoveTimeline
{
    private readonly SlideConfig config;
    private readonly double[] waits;
    private readonly double[] starts;
    private readonly double[] ends;

    /// <summary>
    /// Throws <see cref="ArgumentOutOfRangeException"/> when a time the move uses is out of range: the slide's always,
    /// the blink's only when the move jumps, and the trap's only when one caught the player.
    /// </summary>
    public MoveTimeline(PlayerSlide slide, SlideConfig config)
        : this(slide, config, new double[Math.Max(slide.Runs.Count - 1, 0)])
    {
    }

    private MoveTimeline(PlayerSlide slide, SlideConfig config, double[] waits)
    {
        ArgumentNullException.ThrowIfNull(config);
        Slide = slide;
        this.config = config;
        this.waits = waits;
        var runs = slide.Runs;
        var slideSeconds = SlideTiming.Seconds(slide.Squares, config);
        starts = new double[runs.Count];
        ends = new double[runs.Count];
        var time = 0.0;
        for (var run = 0; run < runs.Count; run++)
        {
            starts[run] = time;
            time += slideSeconds * runs[run].Squares / slide.Squares;
            ends[run] = time;
            if (run < Jumps)
            {
                time += TeleportTiming.Seconds(waits[run], config);
            }
        }
        Seconds = slide.Trapped ? time + TrapTiming.Seconds(config) : time;

        var events = new List<MoveEvent>();
        for (var jump = 0; jump < Jumps; jump++)
        {
            events.Add(new MoveEvent(MoveEventKind.ReachTeleporter, jump, ends[jump]));
            events.Add(new MoveEvent(MoveEventKind.BeginRun, jump + 1, starts[jump + 1]));
        }
        if (slide.Trapped)
        {
            events.Add(new MoveEvent(MoveEventKind.Catch, Jumps, ends[^1]));
        }
        Events = events.AsReadOnly();
    }

    /// <summary>
    /// The move's events in the order they must run, each after the one before even when a single frame passes several:
    /// a run begins before the teleporter it leads to is reached, however short it is. A wait set on a jump moves only
    /// the later ones, so they keep their order.
    /// </summary>
    public IReadOnlyList<MoveEvent> Events { get; }

    public PlayerSlide Slide { get; }

    public int Jumps => Slide.Runs.Count - 1;

    // The whole move, given the waits set so far
    public double Seconds { get; }

    public double RunStart(int run) => starts[CheckRun(run)];

    // When the player reaches the run's last square: a teleporter, the stop, or the trap
    public double RunEnd(int run) => ends[CheckRun(run)];

    /// <summary>
    /// A copy with one jump's camera wait: how long after the player reaches that teleporter the camera's glide to its
    /// partner ends. Throws <see cref="ArgumentOutOfRangeException"/> for a jump that doesn't exist, or a wait that is
    /// negative or not finite.
    /// </summary>
    public MoveTimeline WithWait(int jump, double seconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(jump);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(jump, Jumps);
        var copy = (double[])waits.Clone();
        copy[jump] = seconds;
        return new MoveTimeline(Slide, config, copy);
    }

    public MoveFrame At(double elapsed)
    {
        var runs = Slide.Runs;
        if (elapsed <= 0)
        {
            return new MoveFrame(runs[0].From, runs[0].To, 0, 1, true);
        }
        for (var run = 0; run < runs.Count; run++)
        {
            if (elapsed >= starts[run] && elapsed < ends[run])
            {
                return new MoveFrame(runs[run].From, runs[run].To, (elapsed - starts[run]) / (ends[run] - starts[run]), 1, true);
            }
            if (run < Jumps && elapsed >= ends[run] && elapsed < starts[run + 1])
            {
                var (onExit, scale) = TeleportTiming.At(elapsed - ends[run], waits[run], config);
                var cell = onExit ? runs[run + 1].From : runs[run].To;
                return new MoveFrame(cell, cell, 1, scale, false);
            }
        }
        if (Slide.Trapped && elapsed < Seconds)
        {
            var (onStart, scale) = TrapTiming.At(elapsed - ends[^1], config);
            var cell = onStart ? Slide.To : Slide.Reached;
            return new MoveFrame(cell, cell, 1, scale, false);
        }
        return new MoveFrame(Slide.To, Slide.To, 1, 1, false);
    }

    private int CheckRun(int run)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(run);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(run, Slide.Runs.Count);
        return run;
    }
}
