using System;
using System.Collections.Generic;
using System.Diagnostics;
using Godot;
using MergedCells.Core;

namespace MergedCells;

// Infinite mode's wiring: the setup screen, making each map, and Next map and New map
public partial class Main
{
    private const string NoMapNotice = "Couldn't make a map at these settings. Try another size or difficulty.";

    // Built on first use from the config, on the main thread
    private InfiniteMaps infiniteMaps;
    private int seedCounter;
    // The choice being played, or null outside infinite play
    private InfiniteChoice infiniteChoice;
    // The run being played: made with its first map, kept through Next map and New map, and dropped by any screen change
    private InfiniteRun infiniteRun;
    // The map being made in the background, the screen waiting for it, and what the log line needs
    private MapJob mapJob;
    private IMapWait waiting;
    private InfiniteChoice jobChoice;
    private string jobHeading;
    private readonly Stopwatch jobClock = new();

    // notice: why the last map couldn't be made, or null
    public void ShowInfiniteSetup(string notice = null) => Swap(() =>
    {
        var config = GameConfigFile.Current.Infinite;
        var setup = Open<InfiniteSetup>(InfiniteSetupScene);
        setup.PlayPressed += OnInfinitePlayPressed;
        setup.BackPressed += ShowModeSelect;
        setup.WaitCancelled += () => CancelWait(setup);
        setup.Open(SaveGameFile.Current.Infinite?.Fitted(config) ?? InfiniteChoice.Start(config), config, notice, SaveGameFile.Current.InfiniteBestFor);
        back = ShowModeSelect;
    });

    private void OnInfinitePlayPressed(InfiniteChoice choice)
    {
        SaveGameFile.Current.Infinite = choice;
        SaveGameFile.Save();
        PlayInfinite(choice);
    }

    // The map is made on a thread-pool thread; PollMapJob picks it up. Everything the job needs is read here, on the main thread
    private void PlayInfinite(InfiniteChoice choice)
    {
        var config = GameConfigFile.Current;
        infiniteMaps ??= new InfiniteMaps(config.Infinite, config.Generator, config.Difficulty, config.Climb);
        CancelMapJob();
        var seed = MapSeeds.From(DateTime.UtcNow.Ticks, seedCounter++);
        jobChoice = choice;
        jobHeading = $"Infinite: difficulty {choice.Difficulty}, {choice.Side}×{choice.Side}, {Twists(choice)}, seed {seed}";
        mapJob = MapJob.Start(infiniteMaps, choice, seed);
        jobClock.Restart();
        waiting = screen as IMapWait;
        waiting?.BeginWait();
    }

    // Every frame: the bar once the map has taken longer than the delay, then the map, or the setup screen when none came
    private void PollMapJob()
    {
        if (mapJob is not { } job)
        {
            return;
        }
        if (!job.IsDone)
        {
            if (jobClock.Elapsed.TotalSeconds >= GameConfigFile.Current.Infinite.LoadingDelaySeconds)
            {
                waiting?.ShowWait(job.Progress);
            }
            return;
        }
        // Taken before anything else, so the swaps below don't cancel it
        mapJob = null;
        waiting = null;
        var timing = JobTiming(job);
        if (job.Failure is { } failure)
        {
            GD.PushError($"{jobHeading}: failed {timing}: {failure}");
            ShowInfiniteSetup(NoMapNotice);
            return;
        }
        if (job.Map is not { } map)
        {
            GD.PushWarning($"{jobHeading}: no map {timing}");
            ShowInfiniteSetup(NoMapNotice);
            return;
        }
        var band = GameConfigFile.Current.Infinite.Difficulty(jobChoice.Difficulty);
        var found = map.InBand ? $"score {map.Score} in band" : $"score {map.Score}, nearest to {band.MinScore}–{band.MaxScore}";
        GD.Print($"{jobHeading}: {found}, par {map.Par} {timing}");
        var choice = jobChoice;
        if (screen is Play current && pendingSwap is null)
        {
            StartInfinite(current, choice, map);
            return;
        }
        Swap(() => StartInfinite(OpenPlay(), choice, map));
    }

    // Any screen change stops a map still being made (Swap when it's asked for, RunSwap when it happens), so a map never
    // outlives the screen waiting for it or opens over the one the player went to
    private void CancelMapJob()
    {
        if (mapJob is not { } job)
        {
            return;
        }
        job.Cancel();
        mapJob = null;
        waiting = null;
        GD.Print($"{jobHeading}: cancelled {JobTiming(job)}");
    }

    private void CancelWait(IMapWait screenWaiting)
    {
        CancelMapJob();
        screenWaiting.EndWait();
    }

    private string JobTiming(MapJob job) => $"({jobClock.ElapsedMilliseconds} ms, {Plural(job.StepsTaken, "step")})";

    private void StartInfinite(Play play, InfiniteChoice choice, InfiniteMap map)
    {
        infiniteChoice = choice;
        var config = GameConfigFile.Current;
        infiniteRun ??= new InfiniteRun(config.Infinite.Scoring, config.Stars);
        infiniteRun.StartMap(map.Par);
        var run = infiniteRun;
        play.Start(map.Map, map.Par, new PlaySetup($"Difficulty {choice.Difficulty}")
        {
            Detail = $"{choice.Side}×{choice.Side}",
            HasNext = () => true,
            NextText = "Next map",
            Rules = PlayRules.Unlimited,
            NewMap = true,
            Recording = InfiniteRecording(choice, map),
            ScoreFinish = moves => ScoreInfinite(run, choice, map.Score, moves),
            RunLine = () => InfiniteCard.RunLine(run),
        });
        back = () => ShowInfiniteSetup();
    }

    // Only a best that rose or coins paid write the save, once, and nothing here throws for a finish at or above par
    private InfiniteCard ScoreInfinite(InfiniteRun run, InfiniteChoice choice, int difficultyScore, int moves)
    {
        var score = run.Finish(moves);
        var before = SaveGameFile.Current.InfiniteBestFor(choice);
        var raised = RecordInfiniteBest(run, choice);
        var paid = Wallet.PayMap(score, difficultyScore);
        if (raised || paid > 0)
        {
            SaveGameFile.Save();
        }
        var after = SaveGameFile.Current.InfiniteBestFor(choice) ?? InfiniteBest.None;
        var window = run.WindowAverage is { } average ? $"{average / 100}%" : $"{run.Maps} of {GameConfigFile.Current.Infinite.Scoring.EfficiencyWindow}";
        var kept = score.Counted ? "" : $", kept {Plural(score.KeptMoves, "move")}";
        var coins = paid > 0 ? $", {CoinWords.Earned(paid)} ({CoinWords.Balance(Wallet.Balance)})" : "";
        GD.Print($"Infinite run: map {run.Maps}, {Plural(moves, "move")} ({score.Efficiency / 100}%){kept}, +{score.KeptPoints}, run {run.Score}, streak {run.Streak}, last {window}{(raised ? ", new best" : "")}{coins}");
        return InfiniteCard.Of(score, run, before, after, GameConfigFile.Current.Infinite.Scoring, paid);
    }

    // Whether a best rose, so the caller writes the save
    private static bool RecordInfiniteBest(InfiniteRun run, InfiniteChoice choice) =>
        SaveGameFile.Current.RecordInfiniteBest(choice, run.Best);

    private void OnNewMapPressed()
    {
        if (infiniteChoice is not { } choice)
        {
            return;
        }
        if (infiniteRun is { } run)
        {
            run.GiveUp();
            var raised = RecordInfiniteBest(run, choice);
            if (raised)
            {
                SaveGameFile.Save();
            }
            GD.Print($"Infinite run: New map on map {run.Maps}, run {run.Score}, streak {run.Streak}{(raised ? ", new best" : "")}");
        }
        PlayInfinite(choice);
    }

    // "plain", "walls", "walls and teleporters", "walls, traps and irregular", for the log
    private static string Twists(InfiniteChoice choice)
    {
        var on = new List<string>();
        if (choice.Walls)
        {
            on.Add("walls");
        }
        if (choice.Traps)
        {
            on.Add("traps");
        }
        if (choice.Teleporters)
        {
            on.Add("teleporters");
        }
        if (choice.Irregular)
        {
            on.Add("irregular");
        }
        return on.Count switch
        {
            0 => "plain",
            1 => on[0],
            _ => $"{string.Join(", ", on.GetRange(0, on.Count - 1))} and {on[^1]}",
        };
    }
}
