namespace MergedCells.Core;

/// <summary>
/// What a player's answers mean. A kind is given only by a yes to exactly the wording now shown; it needs asking when
/// it was never asked, or the yes was to other wording. A "no" is never asked again.
/// </summary>
public static class Consents
{
    public static bool IsGiven(Consent? answer, ConsentWording wording)
    {
        ArgumentNullException.ThrowIfNull(wording);
        return answer is { Yes: true } && answer.Wording == wording.Version;
    }

    public static bool NeedsAsking(Consent? answer, ConsentWording wording)
    {
        ArgumentNullException.ThrowIfNull(wording);
        return answer is null || (answer.Yes && answer.Wording != wording.Version);
    }

    /// <summary>The first offered wording, in the order given, whose kind needs asking; null when none does.</summary>
    public static ConsentWording? NextToAsk(SaveGame save, IEnumerable<ConsentWording> wordings)
    {
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(wordings);
        return wordings.FirstOrDefault(wording => wording.Offered && NeedsAsking(save.ConsentTo(wording.Kind), wording));
    }

    /// <summary>Keeps the answer with the wording's version. A yes makes the player ID when there's none yet.</summary>
    public static void Answer(SaveGame save, ConsentWording wording, bool yes, Func<Guid> newPlayer)
    {
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(wording);
        ArgumentNullException.ThrowIfNull(newPlayer);
        save.SetConsent(wording.Kind, new Consent(yes, wording.Version));
        if (yes && save.Player is null)
        {
            save.Player = newPlayer();
        }
    }

    public static bool MayRecord(SaveGame save)
    {
        ArgumentNullException.ThrowIfNull(save);
        return IsGiven(save.ConsentTo(ConsentKind.PlayData), ConsentWordings.PlayData);
    }

    /// <summary>
    /// Whether a finished visit's record may be written: play data still given, and the record under the save's player ID,
    /// so a visit open across "Delete my data" never writes under the deleted ID.
    /// </summary>
    public static bool MayKeep(PlayRecordStart start, SaveGame save)
    {
        ArgumentNullException.ThrowIfNull(start);
        return MayRecord(save) && save.Player == start.Player;
    }
}
