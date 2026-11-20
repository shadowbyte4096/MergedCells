using System;
using Godot;
using MergedCells.Core;

namespace MergedCells;

// Consent: the one-time ask on the mode select, the "Privacy and data" page's switches, and "Delete my data"
public partial class Main
{
    // An ask closed unanswered comes back at most once per run
    private bool askDismissed;

    private void WireConsent(ModeSelect menu)
    {
        menu.Consent.AnswerGuardSeconds = GameConfigFile.Current.Privacy.AskGuardSeconds;
        menu.Consent.Answered += (wording, yes) =>
        {
            Answer(wording, yes);
            // An answer the save couldn't keep holds for this run only, so the mode select says the save isn't being written
            menu.ShowSaveNotice(SaveGameFile.Notice);
            AskNext(menu);
        };
        menu.Consent.Dismissed += () =>
        {
            askDismissed = true;
            GD.Print("Consent: ask closed unanswered");
        };
        AskNext(menu);
    }

    private void AskNext(ModeSelect menu)
    {
        // A save that can't be written couldn't keep the answer, so the ask would come back every run
        if (askDismissed || !SaveGameFile.CanWrite)
        {
            return;
        }
        var save = SaveGameFile.Current;
        if (Consents.NextToAsk(save, ConsentWordings.All) is not { } wording)
        {
            return;
        }
        var changed = save.ConsentTo(wording.Kind) is { Yes: true };
        GD.Print($"Consent: asking about {wording.Kind}{(changed ? ", its wording changed" : "")}");
        menu.Consent.Ask(wording, changed);
    }

    private static void OnConsentChosen(SettingsSheet sheet, ConsentWording wording, bool on)
    {
        if (SaveGameFile.CanWrite)
        {
            Answer(wording, on);
        }
        sheet.Refresh();
    }

    // A yes sends what's waiting at once, as the wording says
    private static void Answer(ConsentWording wording, bool yes)
    {
        Consents.Answer(SaveGameFile.Current, wording, yes, Guid.NewGuid);
        SaveGameFile.Save();
        GD.Print($"Consent: {wording.Kind} {(yes ? "yes" : "no")} (wording {wording.Version})");
        if (yes)
        {
            WakeServer(force: true);
        }
    }

    // The player ID and the records on this phone; every answer stays, so a yes still standing records and shares under a
    // new ID. The old ID goes into the save's pending deletions until the server confirms, never back into the save's
    // player, or a visit still open would write and upload under it (see Consents.MayKeep)
    private static void DeleteMyData(SettingsSheet sheet)
    {
        if (!SaveGameFile.CanWrite)
        {
            return;
        }
        var recorded = PlayRecordFiles.Count;
        var player = SaveGameFile.Current.Player;
        var result = DataDeletion.Run(SaveGameFile.Current, SaveGameFile.Save, PlayRecordFiles.Delete);
        GD.Print($"Play data: delete {result.Outcome} ({recorded} records{(result.ServerPending ? ", the server's copy pending" : "")})");
        sheet.Refresh();
        if (result.Outcome == DeletionOutcome.SaveNotWritten)
        {
            sheet.ShowDeleteResult("Nothing was deleted: the save can't be written. Try again later.");
            return;
        }
        var what = recorded switch
        {
            0 => "your random ID",
            1 => "1 map and your random ID",
            _ => $"{recorded} maps and your random ID",
        };
        string Done(string where) => result.Outcome == DeletionOutcome.RecordsKept
            ? $"Your random ID is deleted{where}, but the maps on this phone couldn't be ({result.Problem?.TrimEnd('.')})."
            : $"Deleted {what}{where}.";
        // A yes still standing shares the bests again under the new ID (decided in chunk 33)
        var again = Consents.IsGiven(SaveGameFile.Current.ConsentTo(ConsentKind.Leaderboards), ConsentWordings.Leaderboards)
            ? " \"Share my scores\" is still on, so your best moves will be shared again under a new random ID."
            : "";
        const string Sent = " For files you've already sent, see the privacy policy.";
        if (!result.ServerPending)
        {
            sheet.ShowDeleteResult(Done("") + again + Sent);
            return;
        }
        sheet.ShowDeleteResult(Done(" on this phone") + " Asking the server to delete what was sent…" + again);
        AwaitServerDeletion(
            sheet,
            player!.Value,
            Done(", here and on the server") + again + Sent,
            Done(" on this phone") + " The server's copy will be deleted the next time you're online." + again + Sent);
    }
}
