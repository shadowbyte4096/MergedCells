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

    private static void Answer(ConsentWording wording, bool yes)
    {
        Consents.Answer(SaveGameFile.Current, wording, yes, Guid.NewGuid);
        SaveGameFile.Save();
        GD.Print($"Consent: {wording.Kind} {(yes ? "yes" : "no")} (wording {wording.Version})");
    }

    // The player ID and the records on this phone; every answer stays, so a yes still standing records under a new ID.
    // Chunk 33 adds the server request here. The old ID goes into a list of pending deletions until the server confirms,
    // never back into the save's player, or a visit still open would write and upload under it (see Consents.MayKeep)
    private static void DeleteMyData(SettingsSheet sheet)
    {
        if (!SaveGameFile.CanWrite)
        {
            return;
        }
        var recorded = PlayRecordFiles.Count;
        var result = DataDeletion.Run(SaveGameFile.Current, SaveGameFile.Save, PlayRecordFiles.Delete);
        GD.Print($"Play data: delete {result.Outcome} ({recorded} records)");
        sheet.Refresh();
        var maps = recorded == 1 ? "1 map" : $"{recorded} maps";
        sheet.ShowDeleteResult(result.Outcome switch
        {
            DeletionOutcome.SaveNotWritten => "Nothing was deleted: the save can't be written. Try again later.",
            DeletionOutcome.RecordsKept => $"Your random ID is deleted, but the maps couldn't be: {result.Problem}",
            _ when recorded == 0 => "Deleted your random ID. For files you've already sent, see the privacy policy.",
            _ => $"Deleted {maps} and your random ID. For files you've already sent, see the privacy policy.",
        });
    }
}
