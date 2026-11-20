namespace MergedCells.Core;

/// <summary>The kinds of player data the game asks about before keeping or sending any.</summary>
public enum ConsentKind
{
    PlayData,
    Leaderboards,
}

/// <summary>A player's answer to one kind's question, and the version of the wording they answered.</summary>
public sealed record Consent
{
    public Consent(bool yes, int wording)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(wording);
        Yes = yes;
        Wording = wording;
    }

    public bool Yes { get; }

    public int Wording { get; }
}

/// <summary>
/// What the player is told and asked about one kind of data, and the version of that wording. Changed is the ask shown
/// to a player whose yes was to another version; the switch's label and note are on the "Privacy and data" page.
/// </summary>
public sealed record ConsentWording(
    ConsentKind Kind,
    int Version,
    bool Offered,
    string AskTitle,
    string Body,
    string Small,
    string No,
    string Yes,
    string ChangedTitle,
    string ChangedLead,
    string ChangedNo,
    string ChangedYes,
    string SwitchLabel,
    string SwitchNote);

public static class ConsentWordings
{
    public const string PolicyUrl = "https://github.com/shadowbyte4096/MergedCells/blob/main/PRIVACY.md";

    // Raise Version with any change to what the player agrees to: a yes to another version is asked again. Version 0 was
    // chunk 22's switch, which said records stay on the phone
    public static readonly ConsentWording PlayData = new(
        ConsentKind.PlayData,
        Version: 1,
        Offered: true,
        AskTitle: "Help tune the puzzles?",
        Body: "Merged Cells can record how you play each map: the map, your moves, times, undos and restarts, with a random ID "
            + "and never your name. Records are sent to the developer's server automatically, and in any file you export. "
            + "They're used only to tune how hard maps are.",
        Small: "Change this or delete your records any time in Settings. If you're under 16, check with a parent first.",
        No: "No thanks",
        Yes: "Record my play",
        ChangedTitle: "Play data has changed",
        ChangedLead: "What's recorded or where it goes has changed, so recording is paused until you answer.",
        ChangedNo: "Stop recording",
        ChangedYes: "Keep recording",
        SwitchLabel: "Record play data",
        SwitchNote: "The map, your moves, times and undos, with a random ID and never your name, for tuning difficulty. Sent to "
            + "the developer's server automatically, and in any file you export.");

    // Its Version is raised the same way
    public static readonly ConsentWording Leaderboards = new(
        ConsentKind.Leaderboards,
        Version: 1,
        Offered: true,
        AskTitle: "Join the leaderboards?",
        Body: "Your best moves on each level go to the leaderboards with a random ID and never your name, so you can see where "
            + "you sit among other players. The spread of everyone's moves also helps tune how hard levels are. Say no and "
            + "leaderboards stay hidden.",
        Small: "Change this or delete your scores any time in Settings. If you're under 16, check with a parent first.",
        No: "No thanks",
        Yes: "Join",
        ChangedTitle: "Leaderboards have changed",
        ChangedLead: "What's shared or where it goes has changed, so sharing is paused until you answer.",
        ChangedNo: "Leave",
        ChangedYes: "Stay in",
        SwitchLabel: "Share my scores",
        SwitchNote: "Your best moves on each level, with a random ID and never your name, for the leaderboards and for tuning "
            + "difficulty. Off hides leaderboards.");

    // In the order they're asked
    public static IReadOnlyList<ConsentWording> All { get; } = [PlayData, Leaderboards];

    public static ConsentWording For(ConsentKind kind) => kind switch
    {
        ConsentKind.PlayData => PlayData,
        ConsentKind.Leaderboards => Leaderboards,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a kind of consent."),
    };
}
