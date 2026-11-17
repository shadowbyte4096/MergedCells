using System;
using Godot;
using MergedCells.Core;

namespace MergedCells;

// A level's small picture, for the custom levels list and the campaign picker: tints with ink lines between blocks (the
// developer's choice over flat tints, which blur at this size), then walls, traps, teleporters as dots, the end and the
// start, drawn from Core's LevelPicture in the board theme's colours. Its side is the UI theme's LevelPicture size
public partial class LevelPictureView : Control
{
    private const string ThemeType = "LevelPicture";

    private Map map;
    private LevelPicture picture;
    private BoardTheme board;

    public Map Map
    {
        set
        {
            map = value;
            Rebuild();
        }
    }

    // Named Board because Control.Theme is the UI theme. Colours are worked out again for each theme, as BoardView does,
    // so a theme with a different number of fills still keeps neighbours apart
    public BoardTheme Board
    {
        set
        {
            board = value;
            Rebuild();
        }
    }

    private void Rebuild()
    {
        picture = map is null || board is null ? null : LevelPicture.Of(map, Math.Max(board.Fills.Length, RectangleColoring.MinimumColors));
        QueueRedraw();
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        var side = GetThemeConstant("size", ThemeType);
        CustomMinimumSize = new Vector2(side, side);
    }

    public override void _Draw()
    {
        if (picture is null || board is null || board.Fills.Length == 0)
        {
            return;
        }
        var pixel = 1 / (float)GetViewport().GetOversampling();
        var square = Math.Min(Size.X, Size.Y) / Math.Max(picture.Rows, picture.Columns);
        var origin = (Size - new Vector2(picture.Columns, picture.Rows) * square) / 2;
        Rect2 Cell(Cell cell, float inset) =>
            new(origin + new Vector2(cell.Column + inset, cell.Row + inset) * square, new Vector2(1 - 2 * inset, 1 - 2 * inset) * square);

        DrawRect(new Rect2(Vector2.Zero, Size), board.Background);
        foreach (var run in picture.Fills)
        {
            DrawRect(new Rect2(origin + new Vector2(run.Column, run.Row) * square, new Vector2(run.Length, 1) * square), board.Fills[run.Color % board.Fills.Length]);
        }

        // A tenth of a square, and never thinner than one pixel on screen
        var line = Math.Max(0.1f * square, pixel);
        foreach (var boundary in picture.Lines)
        {
            var at = origin + new Vector2(boundary.Column, boundary.Row) * square;
            DrawRect(
                boundary.Vertical
                    ? new Rect2(at.X - line / 2, at.Y, line, boundary.Length * square)
                    : new Rect2(at.X, at.Y - line / 2, boundary.Length * square, line),
                board.Outline);
        }
        var mapSize = new Vector2(picture.Columns, picture.Rows) * square;
        DrawRect(new Rect2(origin + new Vector2(line, line) / 2, mapSize - new Vector2(line, line)), board.Outline, filled: false, width: line);

        var middle = square / pixel >= 8;
        foreach (var wall in picture.Walls)
        {
            DrawRect(Cell(wall, 0), board.Wall);
            if (middle)
            {
                DrawRect(Cell(wall, 0.25f), board.WallMiddle);
            }
        }
        var stroke = Math.Max(0.18f * square, 1.2f * pixel);
        foreach (var trap in picture.Traps)
        {
            var box = Cell(trap, 0.2f);
            DrawLine(box.Position, box.End, board.Trap, stroke, antialiased: true);
            DrawLine(new Vector2(box.End.X, box.Position.Y), new Vector2(box.Position.X, box.End.Y), board.Trap, stroke, antialiased: true);
        }
        foreach (var teleporter in picture.Teleporters)
        {
            DrawCircle(Cell(teleporter, 0).GetCenter(), 0.28f * square, board.Teleporter, antialiased: true);
        }
        DrawRect(Cell(picture.End, 0.15f), board.End);
        DrawRect(Cell(picture.Start, 0.2f), board.Outline, filled: false, width: Math.Max(0.14f * square, pixel));
    }
}
