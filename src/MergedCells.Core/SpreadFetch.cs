namespace MergedCells.Core;

public enum SpreadFetchState
{
    Waiting,
    Answered,
    Unavailable,

    // The server is off for the rest of the run (no address, or the save can't keep a session), so nothing can be saved
    // for later either
    Off,
}

/// <summary>
/// One results card's request for a level's spread, made by <see cref="ServerQueue.Fetch"/>. The card polls
/// <see cref="State"/>: it changes once, from waiting to answered (with <see cref="Counts"/>), unavailable or off. A card that
/// goes before the answer cancels it, so it's never sent. Main thread only.
/// </summary>
public sealed class SpreadFetch
{
    internal SpreadFetch(int level, string layout)
    {
        Level = level;
        Layout = layout;
    }

    public int Level { get; }

    public string Layout { get; }

    public SpreadFetchState State { get; private set; } = SpreadFetchState.Waiting;

    public SpreadCounts? Counts { get; private set; }

    internal void Answer(SpreadCounts counts)
    {
        if (State == SpreadFetchState.Waiting)
        {
            Counts = counts;
            State = SpreadFetchState.Answered;
        }
    }

    /// <summary>The card went before the answer: a fetch still waiting is never sent.</summary>
    public void Cancel() => Fail();

    internal void Fail(bool off = false)
    {
        if (State == SpreadFetchState.Waiting)
        {
            State = off ? SpreadFetchState.Off : SpreadFetchState.Unavailable;
        }
    }
}
