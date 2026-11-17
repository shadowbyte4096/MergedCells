using System.Collections.Generic;
using Godot;
using MergedCells.Core;

namespace MergedCells;

// The squares a stroke has crossed so far, and the first square of a teleporter pair waiting for its partner, drawn over
// the board in one node. A direct child of the board, which it reads for each square's place
public partial class StrokeOverlay : Node2D
{
    private IReadOnlyList<Cell> cells = [];
    private Cell? pending;
    private Color color;

    public Color Color
    {
        set
        {
            color = value;
            QueueRedraw();
        }
    }

    public IReadOnlyList<Cell> Cells
    {
        set
        {
            cells = value;
            QueueRedraw();
        }
    }

    public Cell? Pending
    {
        set
        {
            pending = value;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        var board = GetParent<BoardView>();
        foreach (var cell in cells)
        {
            DrawRect(board.CellFillRect(cell), color);
        }
        if (pending is { } square)
        {
            DrawRect(board.CellFillRect(square), color);
        }
    }
}
