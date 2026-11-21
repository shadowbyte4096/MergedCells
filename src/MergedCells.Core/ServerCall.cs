using System.Text.Json;

namespace MergedCells.Core;

public enum CallState
{
    Waiting,
    Answered,
    Unavailable,

    // The server is off for the rest of the run (no address, or the save can't keep a session or player ID)
    Off,
}

/// <summary>
/// One call's answer as it comes, made by <see cref="ServerQueue.Send{T}"/>. Its screen polls <see cref="State"/>, which
/// changes once from waiting. A call cancelled before it's sent is never sent; one cancelled after keeps its save change,
/// since the server has acted on it. Main thread only.
/// </summary>
public sealed class CallHandle<T>
    where T : class
{
    internal CallHandle()
    {
    }

    public CallState State { get; private set; } = CallState.Waiting;

    public T? Answer { get; private set; }

    /// <summary>A handle that's off at once, for a run with no server.</summary>
    public static CallHandle<T> Off()
    {
        var handle = new CallHandle<T>();
        handle.Fail(off: true);
        return handle;
    }

    public void Cancel() => Fail();

    internal void Resolve(T answer)
    {
        if (State == CallState.Waiting)
        {
            Answer = answer;
            State = CallState.Answered;
        }
    }

    internal void Fail(bool off = false)
    {
        if (State == CallState.Waiting)
        {
            State = off ? CallState.Off : CallState.Unavailable;
        }
    }
}

/// <summary>
/// A request to one of the server's functions (<c>rest/v1/rpc/<paramref name="Function"/></c>), sent through
/// <see cref="ServerQueue.Send{T}"/>. <paramref name="Needs"/> is the kind of data that must be given at the moment of
/// sending, or null for a deletion, sent whatever the answers. <paramref name="Arguments"/> writes the named arguments,
/// given the save's player ID; <paramref name="Read"/> reads a 2xx reply's object, null when it isn't in the expected shape.
/// </summary>
public sealed record ServerCall<T>(string Function, ConsentKind? Needs, Action<Utf8JsonWriter, Guid> Arguments, Func<JsonElement, T?> Read)
    where T : class
{
    // False sends it with the publishable key alone, as "Delete my data" is
    public bool Session { get; init; } = true;

    // The player pressed something for it, so it doesn't wait out the gap after a failed round
    public bool Forced { get; init; }

    /// <summary>
    /// The save's change on a readable reply for the same player, applied even when the call was cancelled after it was
    /// sent. Returns whether the save changed, so the queue writes it only then.
    /// </summary>
    public Func<SaveGame, Guid, T, bool>? Apply { get; init; }
}
