using Godot;
using MergedCells.Core;

namespace MergedCells;

public partial class Main : Node2D
{
    public enum DemoBoard
    {
        Sketch,
        Grid50,
        Grid100,
        Grid100CloseUp,
    }

    [Export] public DemoBoard Board { get; set; } = DemoBoard.Sketch;

    public override void _Ready()
    {
        GD.Print("Merged Cells started (C#).");

        var board = GetNode<BoardView>("Board");
        var camera = GetNode<BoardCamera>("Camera");
        RenderingServer.SetDefaultClearColor(board.Theme.Background);
        board.ThemeChanged += () => RenderingServer.SetDefaultClearColor(board.Theme.Background);

        var map = Board switch
        {
            DemoBoard.Sketch => DemoMaps.Sketch(),
            DemoBoard.Grid50 => DemoMaps.Tiled(5),
            _ => DemoMaps.Tiled(10),
        };
        var player = Board == DemoBoard.Sketch ? new Cell(2, 2) : new Cell(map.Rows / 2, map.Columns / 2);
        board.Map = map;
        board.GetNode<StartMarker>("StartMarker").Cell = map.Start;
        board.GetNode<PlayerView>("Player").Cell = player;

        if (Board == DemoBoard.Grid100CloseUp)
        {
            var size = new Vector2(7, 12) * BoardView.CellSize;
            camera.Fit(new Rect2(board.CellCentre(player) - size / 2, size));
        }
        else
        {
            camera.Fit(board.Bounds);
        }
    }
}
