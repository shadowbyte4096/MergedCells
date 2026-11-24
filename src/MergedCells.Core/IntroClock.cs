namespace MergedCells.Core;

// The intro's clock. The frame the intro first draws in adds nothing, and a long frame (a hitch while the menu builds,
// or the app coming back from the background) adds at most MaxStep, so the opening slows for a moment rather than
// skipping ahead
public sealed class IntroClock
{
    public const double MaxStep = 0.05;

    private bool started;

    public double Elapsed { get; private set; }

    public double Tick(double delta)
    {
        if (started)
        {
            Elapsed += Math.Min(delta, MaxStep);
        }
        else
        {
            started = true;
        }
        return Elapsed;
    }
}
