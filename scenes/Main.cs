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
        board.Map = map;
        board.GetNode<StartMarker>("StartMarker").Cell = map.Start;

        // A stand-in until chunk 6's play loop: moves snap the player, with no animation or HUD
        var player = board.GetNode<PlayerView>("Player");
        var attempt = new Attempt(map);
        player.Cell = attempt.Position;
        GetNode<MoveInput>("Input").MoveRequested += direction =>
        {
            var outcome = attempt.Move(direction);
            if (outcome == MoveOutcome.Ignored)
            {
                return;
            }
            player.Cell = attempt.Position;
            var won = outcome == MoveOutcome.Won ? ", won" : "";
            GD.Print($"Move {direction} to [{attempt.Position.Row}, {attempt.Position.Column}]{won}");
        };

        if (Board == DemoBoard.Grid100CloseUp)
        {
            var size = new Vector2(7, 12) * BoardView.CellSize;
            var middle = board.CellCentre(new Cell(map.Rows / 2, map.Columns / 2));
            camera.Fit(new Rect2(middle - size / 2, size));
        }
        else
        {
            camera.Fit(board.Bounds);
        }
    }
}
