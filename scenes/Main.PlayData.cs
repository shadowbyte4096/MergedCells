using System;
using Godot;
using MergedCells.Core;

namespace MergedCells;

// The settings sheet's wiring, and play-data recording on playtest builds: who records, what a record starts with, and
// where it goes
public partial class Main
{
    // Every screen's sheet opens on the saved values and reports each choice here, so no screen keeps its own copy
    private void WireSettings(SettingsSheet sheet)
    {
        sheet.Current = SheetNow;
        sheet.AppearanceChosen += OnAppearanceChosen;
        sheet.RecordingChosen += OnRecordingChosen;
        sheet.ExportPressed += () => PlayRecordFiles.Export(sheet.ShowExportResult);
    }

    private SheetValues SheetNow()
    {
        var playtest = PlaytestBuild.IsOn;
        return new SheetValues(
            SaveGameFile.Current.Appearance,
            SaveGameFile.Current.RecordPlayData,
            playtest,
            playtest ? PlayRecordFiles.Count : 0);
    }

    private void OnRecordingChosen(bool on)
    {
        var save = SaveGameFile.Current;
        save.RecordPlayData = on;
        if (on && save.Playtester is null)
        {
            save.Playtester = Guid.NewGuid();
        }
        SaveGameFile.Save();
        GD.Print($"Setting: record play data {(on ? "on" : "off")}");
    }

    private static bool Recording => PlaytestBuild.IsOn && SaveGameFile.Current.RecordPlayData;

    // Asked at the map's start and at each Retry, so a visit is recorded whenever the switch is on as it starts. The level
    // is rated once, on the first visit recorded, and kept for its Retries
    private static Func<PlayRecorder> CampaignRecording(Level level)
    {
        (DifficultyMetrics Metrics, int Score)? rated = null;
        return Guarded(() =>
        {
            var rater = new DifficultyRater(GameConfigFile.Current.Difficulty);
            if (rated is null)
            {
                var metrics = rater.Measure(level.Map)
                    ?? throw new InvalidOperationException($"Level {level.Id} can't be won, so it can't be rated.");
                rated = (metrics, rater.Score(metrics, DifficultyMode.Campaign));
            }
            return new PlayRecorder(
                NewStart(DifficultyMode.Campaign, level.Map, level.Par, rated.Value.Metrics, rated.Value.Score) with
                {
                    Level = level.Id,
                    SeenBefore = SaveGameFile.Current.BestMoves(level) is not null,
                },
                WriteRecord);
        });
    }

    // The map's own metrics and score from its climb, so nothing is rated twice
    private static Func<PlayRecorder> InfiniteRecording(InfiniteChoice choice, InfiniteMap map) =>
        Guarded(() => new PlayRecorder(
            NewStart(DifficultyMode.Infinite, map.Map, map.Par, map.Metrics, map.Score) with { Infinite = choice },
            WriteRecord));

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

    // Made and saved the first time a record needs one, so every record from this device carries the same ID
    private static Guid Playtester()
    {
        var save = SaveGameFile.Current;
        if (save.Playtester is { } id)
        {
            return id;
        }
        var made = Guid.NewGuid();
        save.Playtester = made;
        SaveGameFile.Save();
        return made;
    }

    private static PlayRecordStart NewStart(DifficultyMode mode, Map map, int par, DifficultyMetrics metrics, int score)
    {
        var config = GameConfigFile.Current.Difficulty;
        return new PlayRecordStart(
            Guid.NewGuid(),
            Playtester(),
            WholeSeconds.Of(DateTimeOffset.UtcNow),
            PlaytestBuild.Build,
            OS.GetName(),
            mode,
            null,
            null,
            null,
            map,
            par,
            DifficultyRater.MetricsVersion,
            config.NearOptimalExtraMoves,
            config.BruteForceLookahead,
            metrics,
            score);
    }

    // A visit that ends after recording was turned off isn't kept: the switch is the player's consent
    private static void WriteRecord(PlayRecord record)
    {
        if (!Recording)
        {
            GD.Print("Play data: dropped (recording off)");
            return;
        }
        // Writing happens as a map is won: a fault here must never reach the results card
        try
        {
            PlayRecordFiles.Append(record);
        }
        catch (Exception exception)
        {
            GD.PushError($"Play data: couldn't record ({exception.Message})");
        }
    }
}
