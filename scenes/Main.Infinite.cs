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

    // notice: why the last map couldn't be made, or null
    public void ShowInfiniteSetup(string notice = null) => Swap(() =>
    {
        var config = GameConfigFile.Current.Infinite;
        var setup = Open<InfiniteSetup>(InfiniteSetupScene);
        setup.PlayPressed += OnInfinitePlayPressed;
        setup.BackPressed += ShowModeSelect;
        setup.Open(SaveGameFile.Current.Infinite?.Fitted(config) ?? InfiniteChoice.Start(config), config, notice);
        back = ShowModeSelect;
    });

    private void OnInfinitePlayPressed(InfiniteChoice choice)
    {
        SaveGameFile.Current.Infinite = choice;
        SaveGameFile.Save();
        PlayInfinite(choice);
    }

    // On the main thread, which small maps allow; chunk 23 moves it to a thread of its own
    private void PlayInfinite(InfiniteChoice choice)
    {
        var config = GameConfigFile.Current;
        infiniteMaps ??= new InfiniteMaps(config.Infinite, config.Generator, config.Difficulty, config.Climb);
        var seed = MapSeeds.From(DateTime.UtcNow.Ticks, seedCounter++);
        var watch = Stopwatch.StartNew();
        var map = infiniteMaps.Next(choice, seed);
        watch.Stop();
        var heading = $"Infinite: difficulty {choice.Difficulty}, {choice.Side}×{choice.Side}, {Twists(choice)}, seed {seed}";
        if (map is null)
        {
            GD.PushWarning($"{heading}: no map ({watch.ElapsedMilliseconds} ms)");
            ShowInfiniteSetup(NoMapNotice);
            return;
        }
        var band = config.Infinite.Difficulty(choice.Difficulty);
        var found = map.InBand ? $"score {map.Score} in band" : $"score {map.Score}, nearest to {band.MinScore}–{band.MaxScore}";
        GD.Print($"{heading}: {found}, par {map.Par} ({watch.ElapsedMilliseconds} ms)");
        if (screen is Play current && pendingSwap is null)
        {
            StartInfinite(current, choice, map);
            return;
        }
        Swap(() => StartInfinite(OpenPlay(), choice, map));
    }

    private void StartInfinite(Play play, InfiniteChoice choice, InfiniteMap map)
    {
        infiniteChoice = choice;
        play.Start(map.Map, map.Par, new PlaySetup($"Difficulty {choice.Difficulty}")
        {
            Detail = $"{choice.Side}×{choice.Side}",
            HasNext = () => true,
            NextText = "Next map",
            Rules = PlayRules.Unlimited,
            NewMap = true,
            Leaderboard = false,
            Recording = InfiniteRecording(choice, map),
        });
        back = () => ShowInfiniteSetup();
    }

    private void OnNewMapPressed()
    {
        if (infiniteChoice is { } choice)
        {
            PlayInfinite(choice);
        }
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
