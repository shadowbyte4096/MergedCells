using System;
using Godot;
using MergedCells.Core;

namespace MergedCells;

// The settings sheet's wiring, and play-data recording: who records, what a record starts with, and where it goes
public partial class Main
{
    // Every screen's sheet opens on the saved values and reports each choice here, so no screen keeps its own copy
    private void WireSettings(SettingsSheet sheet)
    {
        sheet.Current = SheetNow;
        sheet.AppearanceChosen += OnAppearanceChosen;
        sheet.ConsentChosen += (wording, on) => OnConsentChosen(sheet, wording, on);
        sheet.ExportPressed += () => PlayRecordFiles.Export(sheet.ShowExportResult);
        sheet.DeleteConfirmed += () => DeleteMyData(sheet);
    }

    private static SheetValues SheetNow()
    {
        var save = SaveGameFile.Current;
        var recorded = PlayRecordFiles.Count;
        return new SheetValues(
            save.Appearance,
            Consents.IsGiven(save.ConsentTo(ConsentKind.PlayData), ConsentWordings.PlayData),
            Consents.IsGiven(save.ConsentTo(ConsentKind.Leaderboards), ConsentWordings.Leaderboards),
            recorded,
            recorded > 0 || save.Player is not null,
            SaveGameFile.CanWrite,
            SaveGameFile.Notice);
    }

    // A save that can't be written can't keep the answer or the player ID, so nothing is recorded in that run
    private static bool Recording => SaveGameFile.CanWrite && Consents.MayRecord(SaveGameFile.Current);

    private static Func<PlayRecorder> CampaignRecording(Level level)
    {
        var rated = RatedOnce(level.Map, DifficultyMode.Campaign, $"Level {level.Id}");
        return Guarded(() => new PlayRecorder(
            NewStart(DifficultyMode.Campaign, level.Map, level.Par, rated()) with
            {
                Level = level.Id,
                SeenBefore = SaveGameFile.Current.BestMoves(level) is not null,
            },
            WriteRecord));
    }

    private static Func<PlayRecorder> CustomRecording(CustomLevel custom, int par)
    {
        var rated = RatedOnce(custom.Map, DifficultyMode.Custom, $"The custom level \"{custom.Name}\"");
        return Guarded(() => new PlayRecorder(
            NewStart(DifficultyMode.Custom, custom.Map, par, rated()) with { Custom = new CustomPlay(custom.Id, custom.IsImported) },
            WriteRecord));
    }

    // The map's own metrics and score from its climb, so nothing is rated twice
    private static Func<PlayRecorder> InfiniteRecording(InfiniteChoice choice, InfiniteMap map) =>
        Guarded(() => new PlayRecorder(
            NewStart(DifficultyMode.Infinite, map.Map, map.Par, (map.Metrics, map.Score)) with { Infinite = choice },
            WriteRecord));

    // Rated on the first visit recorded and kept for its Retries; asked at the map's start and at each Retry, so a visit is
    // recorded whenever recording is on as it starts
    private static Func<(DifficultyMetrics Metrics, int Score)> RatedOnce(Map map, DifficultyMode mode, string name)
    {
        (DifficultyMetrics Metrics, int Score)? rated = null;
        return () =>
        {
            if (rated is null)
            {
                var rater = new DifficultyRater(GameConfigFile.Current.Difficulty);
                var metrics = rater.Measure(map) ?? throw new InvalidOperationException($"{name} can't be won, so it can't be rated.");
                rated = (metrics, rater.Score(metrics, mode));
            }
            return rated.Value;
        };
    }

    // Null while recording is off, and never a throw: play data is for tuning, and must never stop a map starting
    private static Func<PlayRecorder> Guarded(Func<PlayRecorder> start) => () =>
    {
        if (!Recording)
        {
            return null;
        }
        try
        {
            return start();
        }
        catch (Exception exception)
        {
            GD.PushError($"Play data: couldn't start a record ({exception.Message})");
            return null;
        }
    };

    // Made at the first yes; made here only when a yes outlived "Delete my data", so every record carries the save's ID
    private static Guid Player()
    {
        var save = SaveGameFile.Current;
        if (save.Player is { } id)
        {
            return id;
        }
        var made = Guid.NewGuid();
        save.Player = made;
        SaveGameFile.Save();
        return made;
    }

    private static PlayRecordStart NewStart(DifficultyMode mode, Map map, int par, (DifficultyMetrics Metrics, int Score) rated)
    {
        var config = GameConfigFile.Current.Difficulty;
        return new PlayRecordStart(
            Guid.NewGuid(),
            Player(),
            WholeSeconds.Of(DateTimeOffset.UtcNow),
            BuildName.Current,
            OS.GetName(),
            ConsentWordings.PlayData.Version,
            mode,
            null,
            null,
            null,
            null,
            map,
            par,
            DifficultyRater.MetricsVersion,
            config.NearOptimalExtraMoves,
            config.BruteForceLookahead,
            rated.Metrics,
            rated.Score);
    }

    // A visit that ends after recording was turned off, or after "Delete my data" dropped its ID, isn't kept: the switch is
    // the player's consent
    private static void WriteRecord(PlayRecord record)
    {
        if (!SaveGameFile.CanWrite || !Consents.MayKeep(record.Start, SaveGameFile.Current))
        {
            GD.Print("Play data: dropped (recording off, or its ID deleted)");
            return;
        }
        // Writing happens as a map is won: a fault here must never reach the results card
        try
        {
            PlayRecordFiles.Append(record);
            WakeServer();
        }
        catch (Exception exception)
        {
            GD.PushError($"Play data: couldn't record ({exception.Message})");
        }
    }
}
