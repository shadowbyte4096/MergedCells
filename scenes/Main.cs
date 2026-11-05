using System;
using Godot;
using MergedCells.Core;

namespace MergedCells;

// A stand-in until chunk 7 loads levels from files: picks a demo board, solves it for par and starts Play
public partial class Main : Node
{
    public enum DemoBoard
    {
        Sketch,
        Grid50,
        Grid100,
        Grid100CloseUp,
    }

    // System follows the device. Chunk 10's saved setting sets this; until then the editor export is a test override
    public enum AppearanceChoice
    {
        System,
        Light,
        Dark,
    }

    private Play play;
    private bool? appliedDark;

    [Export] public DemoBoard Board { get; set; } = DemoBoard.Sketch;

    [Export] public AppearanceChoice Appearance { get; set; } = AppearanceChoice.System;

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

        var map = Board switch
        {
            DemoBoard.Sketch => DemoMaps.Sketch(),
            DemoBoard.Grid50 => DemoMaps.Tiled(5),
            _ => DemoMaps.Tiled(10),
        };
        var par = Solver.Solve(map).Par ?? throw new InvalidOperationException($"The {Board} demo map can't be solved.");

        var title = Board switch
        {
            DemoBoard.Sketch => "Sketch",
            DemoBoard.Grid50 => "Grid 50",
            DemoBoard.Grid100 => "Grid 100",
            _ => "Grid 100 close-up",
        };
        play.Start(map, par, title);

        if (Board == DemoBoard.Grid100CloseUp)
        {
            var board = play.GetNode<BoardView>("Board");
            var size = new Vector2(7, 12) * BoardView.CellSize;
            var middle = board.CellCentre(new Cell(map.Rows / 2, map.Columns / 2));
            play.GetNode<BoardCamera>("Camera").Fit(new Rect2(middle - size / 2, size));
        }
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
