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

    private Play play;
    private bool? appliedDark;
    private IReadOnlyList<Level> levels;
    private int current;

    [Export] public AppearanceChoice Appearance { get; set; } = AppearanceChoice.System;

    // A 1-based position in the level list, for starting later levels while testing. It goes with Main in chunk 11
    [Export] public int FirstLevel { get; set; } = 1;

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
        StartLevel(Math.Clamp(FirstLevel, 1, levels.Count) - 1);
    }

    private void StartLevel(int index)
    {
        current = index;
        var level = levels[index];
        GD.Print($"Level {index + 1}: {level.Name} (id {level.Id}, par {level.Par})");
        play.Start(level.Map, level.Par, $"{index + 1} · {level.Name}", hasNext: index + 1 < levels.Count);
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
