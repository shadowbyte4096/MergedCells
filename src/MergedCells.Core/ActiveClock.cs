namespace MergedCells.Core;

/// <summary>
/// Time spent playing a map, for play records: the frame times while the app is in front. The first frame after a reset
/// (it includes making the map) and the first after resuming (Godot's first frame back spans the whole time away) are
/// dropped. Main thread only.
/// </summary>
public sealed class ActiveClock
{
    private double seconds;
    private bool running = true;
    private bool skipNext = true;

    public long Milliseconds => (long)(seconds * 1000);

    public void Tick(double delta)
    {
        if (!running)
        {
            return;
        }
        if (skipNext)
        {
            skipNext = false;
            return;
        }
        seconds += delta;
    }

    public void Pause() => running = false;

    // Godot also reports focus coming back when it never went, which mustn't drop a real frame
    public void Resume()
    {
        if (running)
        {
            return;
        }
        running = true;
        skipNext = true;
    }

    public void Reset()
    {
        seconds = 0;
        skipNext = true;
    }
}
