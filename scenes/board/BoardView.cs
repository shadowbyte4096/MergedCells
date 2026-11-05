using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MergedCells.Core;

namespace MergedCells;

public partial class BoardView : Node2D
{
    // Board units per square. Not tunable: the camera scales the whole board
    public const float CellSize = 64f;

    // Points per rounded corner of an irregular region: a drawing detail, not a style
    private const int ArcSegments = 8;

    private BoardTheme theme;
    private Map map;
    private IReadOnlyList<int> colors;
    private int colorCount;
    private CellRange? visible;
    private StyleBoxFlat[] fillBoxes;
    private StyleBoxFlat dotBox;
    // One entry per Map.Rectangles index; null for regions that are rectangles
    private IrregularShape[] irregular;

    public event Action ThemeChanged;

    [Export]
    public BoardTheme Theme
    {
        get => theme;
        set
        {
            theme = value;
            if (theme != null)
            {
                BuildBoxes();
                if (colors == null || theme.Fills.Length != colorCount)
                {
                    Recolor();
                }
            }
            BuildIrregularShapes();
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
            BuildIrregularShapes();
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

    public override void _Process(double delta) => UpdateView();

    // Works out the visible range and whether gaps show. Also called straight after the camera fits a new map,
    // so the first frame of a new level doesn't draw the previous level's range.
    public void UpdateView()
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
        // By ID: an irregular region's bounding rectangle can start on the end square
        var endRegion = map.RegionAt(map.End);
        // In Map.Rectangles order, so a ring is drawn before the regions inside it
        for (var index = 0; index < map.Rectangles.Count; index++)
        {
            var rectangle = map.Rectangles[index];
            if (!rectangle.Overlaps(range))
            {
                continue;
            }
            if (irregular?[index] is { } shape)
            {
                DrawIrregular(rectangle, shape, theme.Fills[colors[index]], range);
                continue;
            }
            var area = new Rect2(
                rectangle.TopLeft.Column * CellSize + half,
                rectangle.TopLeft.Row * CellSize + half,
                rectangle.Columns * CellSize - Gap,
                rectangle.Rows * CellSize - Gap);
            var isEnd = rectangle.Id == endRegion;
            if (Gap > 0)
            {
                DrawStyleBox(fillBoxes[colors[index]], area);
                if (isEnd)
                {
                    DrawEndDots();
                }
            }
            else
            {
                DrawRect(area, isEnd ? theme.End : theme.Fills[colors[index]]);
            }
        }
    }

    private void DrawIrregular(Rectangle bounds, IrregularShape shape, Color fill, CellRange range)
    {
        if (Gap > 0)
        {
            for (var loop = 0; loop < shape.Loops.Length; loop++)
            {
                if (!shape.Holes[loop])
                {
                    DrawColoredPolygon(shape.Loops[loop], fill);
                }
            }
            // A hole shows the background, and whatever lies inside it is drawn later
            for (var loop = 0; loop < shape.Loops.Length; loop++)
            {
                if (shape.Holes[loop])
                {
                    DrawColoredPolygon(shape.Loops[loop], theme.Background);
                }
            }
            foreach (var outline in shape.Closed)
            {
                DrawPolyline(outline, theme.Outline, shape.OutlineWidth, true);
            }
            return;
        }

        // Zoomed out: flat squares edge to edge, like the flat rectangles
        var top = Math.Max(bounds.TopLeft.Row, range.Top);
        var bottom = Math.Min(bounds.BottomRight.Row, range.Bottom);
        var left = Math.Max(bounds.TopLeft.Column, range.Left);
        var right = Math.Min(bounds.BottomRight.Column, range.Right);
        for (var row = top; row <= bottom; row++)
        {
            for (var column = left; column <= right; column++)
            {
                if (map.RegionAt(new Cell(row, column)) == bounds.Id)
                {
                    DrawRect(new Rect2(column * CellSize, row * CellSize, CellSize, CellSize), fill);
                }
            }
        }
    }

    // The outline's centre line serves as the fill's edge too, so the fill's unsmoothed edge sits under the antialiased
    // outline. The band runs from half a gap to half a gap plus the outline width in from the grid line, and its outer
    // corner radius matches the StyleBoxFlat rectangles, whose sizes are rounded the same way (see BuildBoxes).
    private void BuildIrregularShapes()
    {
        irregular = null;
        if (map == null || theme == null)
        {
            return;
        }
        var gapHalf = theme.GapFraction / 2;
        var outline = Mathf.RoundToInt(theme.OutlineFraction * CellSize) / CellSize;
        var corner = Mathf.Min(Mathf.RoundToInt(theme.CornerFraction * CellSize) / CellSize, 0.5f - gapHalf);
        irregular = new IrregularShape[map.Rectangles.Count];
        for (var index = 0; index < irregular.Length; index++)
        {
            var id = map.Rectangles[index].Id;
            if (map.IsRectangle(id))
            {
                continue;
            }
            var loops = RegionOutline.Trace(map, id);
            var points = loops
                .Select(loop => RegionOutline.Inset(loop, gapHalf + outline / 2, Mathf.Max(0, corner - outline / 2), ArcSegments)
                    .Select(point => new Vector2(point.X, point.Y) * CellSize)
                    .ToArray())
                .ToArray();
            irregular[index] = new IrregularShape(
                points,
                loops.Select(loop => loop.IsHole).ToArray(),
                points.Select(loop => loop.Append(loop[0]).ToArray()).ToArray(),
                outline * CellSize);
        }
    }

    private sealed record IrregularShape(Vector2[][] Loops, bool[] Holes, Vector2[][] Closed, float OutlineWidth);

    private void DrawEndDots()
    {
        var centre = CellCentre(map.End);
        var side = theme.EndDotFraction * CellSize;
        var spacing = theme.EndDotSpacing * CellSize;
        for (var row = -1; row <= 1; row++)
        {
            for (var column = -1; column <= 1; column++)
            {
                var dotCentre = centre + new Vector2(column * spacing, row * spacing);
                DrawStyleBox(dotBox, new Rect2(dotCentre - new Vector2(side, side) / 2, side, side));
            }
        }
    }

    // StyleBoxFlat takes whole-number borders and corners, in board units
    private void BuildBoxes()
    {
        var border = Mathf.RoundToInt(theme.OutlineFraction * CellSize);
        var corner = Mathf.RoundToInt(theme.CornerFraction * CellSize);
        fillBoxes = new StyleBoxFlat[theme.Fills.Length];
        for (var i = 0; i < fillBoxes.Length; i++)
        {
            fillBoxes[i] = RoundedBox(theme.Fills[i], corner);
            fillBoxes[i].BorderColor = theme.Outline;
            fillBoxes[i].SetBorderWidthAll(border);
        }
        dotBox = RoundedBox(theme.End, Mathf.RoundToInt(theme.EndDotCornerFraction * theme.EndDotFraction * CellSize));
    }

    private static StyleBoxFlat RoundedBox(Color fill, int corner)
    {
        var box = new StyleBoxFlat { BgColor = fill, AntiAliasing = true };
        box.SetCornerRadiusAll(corner);
        return box;
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
