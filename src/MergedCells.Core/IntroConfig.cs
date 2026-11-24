namespace MergedCells.Core;

/// <summary>The config's <c>intro</c> group: how long each part of the startup intro takes.</summary>
public sealed record IntroConfig
{
    public required double OpenSeconds { get; init; }

    public required double TitleSeconds { get; init; }

    public required double HoldSeconds { get; init; }

    public required double HandoffSeconds { get; init; }

    // A reduced-motion setting should skip the intro rather than set these to 0
    public void CheckRanges()
    {
        CheckSeconds(OpenSeconds, "intro.openSeconds");
        CheckSeconds(TitleSeconds, "intro.titleSeconds");
        CheckSeconds(HoldSeconds, "intro.holdSeconds");
        CheckSeconds(HandoffSeconds, "intro.handoffSeconds");
    }

    private static void CheckSeconds(double seconds, string name)
    {
        if (!double.IsFinite(seconds) || seconds <= 0)
        {
            throw new ArgumentOutOfRangeException(name, seconds, "Must be positive.");
        }
    }
}
