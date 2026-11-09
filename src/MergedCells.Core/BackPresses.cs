namespace MergedCells.Core;

/// <summary>
/// Turns Android's back requests into one per press. Godot 4.7.2 sends a go-back request for every back key event (the
/// press, each auto-repeat and the release), so one tap arrived as two requests and a long press as three. A press runs
/// from the key going down to it coming up, and counts once. Requests that come with no key events (a back gesture that
/// doesn't send keys) each count. Frames are the process frame each call arrives in.
/// </summary>
public sealed class BackPresses
{
    private bool held;
    private bool pressCounted;
    private ulong? countedFrame;
    private ulong? releaseFrame;

    public void KeyDown(ulong frame)
    {
        held = true;
        // The press's request can arrive just before its key, in the same frame
        pressCounted = countedFrame == frame;
    }

    public void KeyUp(ulong frame)
    {
        held = false;
        releaseFrame = frame;
    }

    // Forgets a press in progress, for when its release may never arrive (the app losing focus mid-press)
    public void Reset()
    {
        held = false;
        pressCounted = false;
        countedFrame = null;
        releaseFrame = null;
    }

    // Whether this request is a new back press to act on
    public bool Request(ulong frame)
    {
        if (held || frame == releaseFrame)
        {
            if (pressCounted)
            {
                return false;
            }
            pressCounted = true;
        }
        countedFrame = frame;
        return true;
    }
}
