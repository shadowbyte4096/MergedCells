using System;
using System.Collections.Generic;
using Godot;
using MergedCells.Core;

namespace MergedCells;

public partial class BoardView : Node2D
{
    // Board units per square. Not tunable: the camera scales the whole board
    public const float CellSize = 64f;

    private BoardTheme theme;
    private Map map;
    private IReadOnlyList<int> colors;
    private int colorCount;
    private CellRange? visible;

    public event Action ThemeChanged;

    [Export]
    public BoardTheme Theme
    {
        get => theme;
        set
        {
            theme = value;
            if (theme != null && (colors == null || theme.Fills.Length != colorCount))
            {
                Recolor();
            }
            QueueRedraw();
            ThemeChanged?.Invoke();
        }
    }

    public Map Map
    {
        get => map;
        set
        {
            map = value;
            Recolor();
            QueueRedraw();
        }
    }

    // In board units; 0 while the gaps would be too thin to draw
    public float Gap { get; private set; }

    // The map plus its half-gap frame, whether or not the gaps are showing, so a camera fit doesn't jump
    public Rect2 Bounds
    {
        get
        {
            var half = theme.GapFraction * CellSize / 2;
            return new Rect2(-half, -half, map.Columns * CellSize + 2 * half, map.Rows * CellSize + 2 * half);
        }
    }

    public Vector2 CellCentre(Cell cell) => new((cell.Column + 0.5f) * CellSize, (cell.Row + 0.5f) * CellSize);

    public Rect2 CellFillRect(Cell cell) =>
        new(cell.Column * CellSize + Gap / 2, cell.Row * CellSize + Gap / 2, CellSize - Gap, CellSize - Gap);

    public override void _Process(double delta)
    {
        if (map == null || theme == null)
        {
            return;
        }

        var toViewport = GetGlobalTransformWithCanvas();
        var fullGap = theme.GapFraction * CellSize;
        var gap = fullGap * toViewport.Scale.X >= theme.MinGapPixels ? fullGap : 0f;
        var view = toViewport.AffineInverse() * GetViewportRect();
        var range = CellRange.Covering(
            view.Position.X / CellSize, view.Position.Y / CellSize, view.End.X / CellSize, view.End.Y / CellSize, map.Rows, map.Columns);

        if (gap != Gap)
        {
            Gap = gap;
            QueueRedraw();
            foreach (var child in GetChildren())
            {
                if (child is CanvasItem item)
                {
                    item.QueueRedraw();
                }
            }
        }
        if (range != visible)
        {
            visible = range;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        if (map == null || theme == null || colors == null || visible is not { } range)
        {
            return;
        }

        var half = Gap / 2;
        if (Gap > 0)
        {
            DrawRect(new Rect2(-half, -half, map.Columns * CellSize + Gap, map.Rows * CellSize + Gap), theme.Gap);
        }
        for (var index = 0; index < map.Rectangles.Count; index++)
        {
            var rectangle = map.Rectangles[index];
            if (!rectangle.Overlaps(range))
            {
                continue;
            }
            var fill = rectangle.TopLeft == map.End ? theme.End : theme.Fills[colors[index]];
            DrawRect(
                new Rect2(
                    rectangle.TopLeft.Column * CellSize + half,
                    rectangle.TopLeft.Row * CellSize + half,
                    rectangle.Columns * CellSize - Gap,
                    rectangle.Rows * CellSize - Gap),
                fill);
        }
    }

    private void Recolor()
    {
        colors = null;
        if (map == null || theme == null)
        {
            return;
        }
        colorCount = theme.Fills.Length;
        if (colorCount < RectangleColoring.MinimumColors)
        {
            GD.PushError($"The board theme has {colorCount} fills; it needs at least {RectangleColoring.MinimumColors}.");
            return;
        }
        colors = RectangleColoring.Assign(map, colorCount);
    }
}
