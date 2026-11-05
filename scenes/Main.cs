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

    [Export] public DemoBoard Board { get; set; } = DemoBoard.Sketch;

    public override void _Ready()
    {
        GD.Print("Merged Cells started (C#).");

        var map = Board switch
        {
            DemoBoard.Sketch => DemoMaps.Sketch(),
            DemoBoard.Grid50 => DemoMaps.Tiled(5),
            _ => DemoMaps.Tiled(10),
        };
        var par = Solver.Solve(map).Par ?? throw new InvalidOperationException($"The {Board} demo map can't be solved.");

        var play = GetNode<Play>("Play");
        play.Start(map, par);

        if (Board == DemoBoard.Grid100CloseUp)
        {
            var board = play.GetNode<BoardView>("Board");
            var size = new Vector2(7, 12) * BoardView.CellSize;
            var middle = board.CellCentre(new Cell(map.Rows / 2, map.Columns / 2));
            play.GetNode<BoardCamera>("Camera").Fit(new Rect2(middle - size / 2, size));
        }
    }
}
