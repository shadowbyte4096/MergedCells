using System;
using System.Collections.Generic;
using Godot;
using MergedCells.Core;

namespace MergedCells;

// Plays the level files in order, a stand-in until chunk 11's menus replace it
public partial class Main : Node
{
    // System follows the device. Chunk 10's saved setting sets this; until then the editor export is a test override
    public enum AppearanceChoice
    {
        System,
        Light,
        Dark,
    }

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

    [Export] public AppearanceChoice Appearance { get; set; } = AppearanceChoice.System;

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
        play = GetNode<Play>("Play");
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
            GD.Print($"Level {index + 1}: {level.Name} (id {level.Id}, par {level.Par})");
            play.Start(level.Map, level.Par, $"{index + 1} · {level.Name}", hasNext);
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

    private void ResetFrameLog()
    {
        frameWindow = 0;
        frameWorst = 0;
        frameCount = 0;
    }

    private void ApplyAppearance()
    {
        if (play == null)
        {
            return;
        }
        var dark = Appearance switch
        {
            AppearanceChoice.Light => false,
            AppearanceChoice.Dark => true,
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
