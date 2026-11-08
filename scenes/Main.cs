using System;
using System.Collections.Generic;
using Godot;
using MergedCells.Core;

namespace MergedCells;

// Plays the level files in order, a stand-in until chunk 11's menus replace it
public partial class Main : Node
{
    // The frame-time log's window on test boards: a test detail, not a tunable number
    private const double FrameLogSeconds = 5;

    private Play play;
    private bool? appliedDark;
    private IReadOnlyList<Level> levels;
    private int current;
    private bool onTestBoard;
    private bool refreshLogged;
    private double frameWindow;
    private double frameWorst;
    private int frameCount;
    private int framesToSkip;

    // A 1-based position in the level list, then the test boards, for starting later ones while testing.
    // It goes with Main in chunk 11
    [Export] public int FirstLevel { get; set; } = 1;

    private int Count => levels.Count + TestBoards.All.Count;

    public override void _Notification(int what)
    {
        if (what == NotificationApplicationFocusIn)
        {
            ApplyAppearance();
        }
    }

    public override void _Ready()
    {
        GD.Print("Merged Cells started (C#).");
        // Read first, so the first frame is already in the saved appearance
        _ = SaveGameFile.Current;
        play = GetNode<Play>("Play");
        play.Appearance = SaveGameFile.Current.Appearance;
        play.AppearanceChosen += OnAppearanceChosen;
        ApplyAppearance();
        // Not every platform reports theme changes, so focus changes re-check too
        DisplayServer.SetSystemThemeChangeCallback(Callable.From(ApplyAppearance));

        levels = LevelFiles.LoadAll();
        play.NextPressed += () => StartLevel(current + 1);
        StartLevel(Math.Clamp(FirstLevel, 1, Count) - 1);
    }

    // On test boards only, for measuring drawing cost on a phone through logcat
    public override void _Process(double delta)
    {
        if (!onTestBoard)
        {
            return;
        }
        // The frames that build, colour and first draw a new board aren't what's being measured
        if (framesToSkip > 0)
        {
            framesToSkip--;
            return;
        }
        frameWindow += delta;
        frameWorst = Math.Max(frameWorst, delta);
        frameCount++;
        if (frameWindow >= FrameLogSeconds)
        {
            GD.Print($"Frames: average {frameWindow / frameCount * 1000:0.0} ms, worst {frameWorst * 1000:0.0} ms over the last {FrameLogSeconds:0} s");
            ResetFrameLog();
        }
    }

    private void StartLevel(int index)
    {
        current = index;
        var hasNext = index + 1 < Count;
        ResetFrameLog();
        framesToSkip = 2;
        onTestBoard = index >= levels.Count;
        if (!onTestBoard)
        {
            var level = levels[index];
            var save = SaveGameFile.Current;
            var played = save.BestMoves(level) is { } best
                ? $"best {best}, {Plural(save.Stars(level, GameConfigFile.Current.Stars) ?? 0, "star")}"
                : "not played";
            GD.Print($"Level {index + 1}: {level.Name} (id {level.Id}, par {level.Par}, {played})");
            play.Start(level.Map, level.Par, $"{index + 1} · {level.Name}", hasNext, moves => RecordFinish(level, moves));
            return;
        }

        var (title, build) = TestBoards.All[index - levels.Count];
        var map = build();
        var par = Solver.Solve(map).Par ?? throw new InvalidOperationException($"The test board \"{title}\" can't be solved.");
        if (!refreshLogged)
        {
            refreshLogged = true;
            GD.Print($"Screen refresh: {DisplayServer.ScreenGetRefreshRate():0} Hz");
        }
        GD.Print($"Level {index + 1}: {title} (test board, par {par})");
        play.Start(map, par, title, hasNext);
    }

    // Asks the save at each finish, so a Retry compares with the best saved a moment ago
    private static LevelFinish RecordFinish(Level level, int moves)
    {
        var finish = SaveGameFile.Current.RecordFinish(level, moves, GameConfigFile.Current.Stars);
        if (finish.IsNewBest)
        {
            SaveGameFile.Save();
            var was = finish.PreviousBest is { } previous ? $"was {previous}" : "first finish";
            GD.Print($"New best: {Plural(moves, "move")} ({was})");
        }
        return finish;
    }

    private static string Plural(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";

    private void ResetFrameLog()
    {
        frameWindow = 0;
        frameWorst = 0;
        frameCount = 0;
    }

    private void OnAppearanceChosen(Appearance choice)
    {
        SaveGameFile.Current.Appearance = choice;
        SaveGameFile.Save();
        GD.Print($"Setting: appearance {choice.ToString().ToLowerInvariant()}");
        ApplyAppearance();
    }

    private void ApplyAppearance()
    {
        if (play == null)
        {
            return;
        }
        // The saved choice wins over the device unless it's System
        var dark = SaveGameFile.Current.Appearance switch
        {
            Appearance.Light => false,
            Appearance.Dark => true,
            _ => DisplayServer.IsDarkModeSupported() && DisplayServer.IsDarkMode(),
        };
        if (appliedDark == dark)
        {
            return;
        }
        appliedDark = dark;
        play.Dark = dark;
        GD.Print($"Appearance: {(dark ? "dark" : "light")}");
    }
}
