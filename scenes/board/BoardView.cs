using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Godot;
using MergedCells.Core;

namespace MergedCells;

// Draws the board in blocks of squares, each built once into its own canvas item and reused while the camera moves.
// Godot skips blocks whose drawing is off screen, so only roughly what's visible is sent each frame.
public partial class BoardView : Node2D
{
    // Board units per square. Not tunable: the camera scales the whole board
    public const float CellSize = 64f;

    // Points per rounded corner of an irregular region: a drawing detail, not a style
    private const int ArcSegments = 8;

    // Squares per block side, and how long each frame may spend building blocks ahead of need: drawing details, tuned
    // against the phone, not game feel
    private const int BlockSize = 8;
    private const double BuildBudgetMs = 3.0;

    private readonly Stopwatch buildClock = new();
    private BoardTheme theme;
    private Map map;
    private IReadOnlyList<int> colors;
    private int colorCount;
    private StyleBoxFlat[] fillBoxes;
    private StyleBoxFlat dotBox;
    // One entry per Map.Rectangles index; null for regions that are rectangles
    private IrregularShape[] irregular;
    private BoardBlocks blocks;
    // Each block twice: with outlines (gaps showing) and zoomed out (flat tints edge to edge)
    private Rid[] outlinedItems;
    private Rid[] flatItems;
    private bool[] outlinedBuilt;
    private bool[] flatBuilt;
    private int unbuilt;
    // StyleBoxFlat divides its antialiasing size by the viewport's oversampling (the stretch scale) only inside a
    // node's _Draw, and blocks are built outside one, so the boxes carry that division themselves (see RoundedBox)
    private float boxOversampling = 1;

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
            MarkUnbuilt();
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
            CreateBlocks();
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

    public override void _Notification(int what)
    {
        if (what == NotificationPredelete)
        {
            FreeBlocks();
        }
    }

    // Works out whether gaps show and builds what's on screen. Also called straight after the camera fits a new map,
    // so the first frame of a new level draws the right part of it.
    public void UpdateView()
    {
        if (map == null || theme == null)
        {
            return;
        }

        var oversampling = (float)GetViewport().GetOversampling();
        if (oversampling != boxOversampling)
        {
            boxOversampling = oversampling;
            BuildBoxes();
            MarkUnbuilt();
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
            ShowVersion();
            foreach (var child in GetChildren())
            {
                if (child is CanvasItem item)
                {
                    item.QueueRedraw();
                }
            }
        }
        Build(range);
    }

    // Builds every block drawing on screen in the version being shown, then more, nearest first, within the budget
    private void Build(CellRange? range)
    {
        if (blocks == null || colors == null || unbuilt == 0)
        {
            return;
        }
        var outlined = Gap > 0;
        if (range is { } onScreen)
        {
            foreach (var block in blocks.Drawing(onScreen))
            {
                BuildIfNeeded(block, outlined);
            }
        }

        buildClock.Restart();
        var order = blocks.BuildOrder(range ?? new CellRange(0, 0, map.Rows - 1, map.Columns - 1));
        foreach (var version in new[] { outlined, !outlined })
        {
            foreach (var block in order)
            {
                if (unbuilt == 0 || buildClock.Elapsed.TotalMilliseconds >= BuildBudgetMs)
                {
                    return;
                }
                BuildIfNeeded(block, version);
            }
        }
    }

    private void BuildIfNeeded(int block, bool outlined)
    {
        var built = outlined ? outlinedBuilt : flatBuilt;
        if (built[block])
        {
            return;
        }
        BuildBlock(block, outlined);
        built[block] = true;
        unbuilt--;
    }

    private void BuildBlock(int block, bool outlined)
    {
        var item = outlined ? outlinedItems[block] : flatItems[block];
        RenderingServer.CanvasItemClear(item);
        // The outlined version always uses the full gap: it may be built while zoomed out, ahead of need
        var half = outlined ? theme.GapFraction * CellSize / 2 : 0;
        // By ID: an irregular region's bounding rectangle can start on the end square
        var endRegion = map.RegionAt(map.End);
        foreach (var index in blocks.Regions(block))
        {
            var rectangle = map.Rectangles[index];
            var fill = theme.Fills[colors[index]];
            if (irregular?[index] is { } shape)
            {
                if (outlined)
                {
                    DrawIrregular(item, shape, fill);
                }
                else
                {
                    DrawFlatSquares(item, rectangle, fill);
                }
                continue;
            }
            var area = new Rect2(
                rectangle.TopLeft.Column * CellSize + half,
                rectangle.TopLeft.Row * CellSize + half,
                rectangle.Columns * CellSize - 2 * half,
                rectangle.Rows * CellSize - 2 * half);
            var isEnd = rectangle.Id == endRegion;
            if (outlined)
            {
                fillBoxes[colors[index]].Draw(item, area);
                if (isEnd)
                {
                    DrawEndDots(item);
                }
            }
            else
            {
                RenderingServer.CanvasItemAddRect(item, area, isEnd ? theme.End : fill);
            }
        }
    }

    private void DrawIrregular(Rid item, IrregularShape shape, Color fill)
    {
        for (var loop = 0; loop < shape.Loops.Length; loop++)
        {
            if (!shape.Holes[loop])
            {
                RenderingServer.CanvasItemAddPolygon(item, shape.Loops[loop], new[] { fill });
            }
        }
        // A hole shows the background, and whatever lies inside it is drawn later
        for (var loop = 0; loop < shape.Loops.Length; loop++)
        {
            if (shape.Holes[loop])
            {
                RenderingServer.CanvasItemAddPolygon(item, shape.Loops[loop], new[] { theme.Background });
            }
        }
        foreach (var outline in shape.Closed)
        {
            RenderingServer.CanvasItemAddPolyline(item, outline, new[] { theme.Outline }, shape.OutlineWidth, true);
        }
    }

    // Zoomed out: flat squares edge to edge, like the flat rectangles
    private void DrawFlatSquares(Rid item, Rectangle bounds, Color fill)
    {
        for (var row = bounds.TopLeft.Row; row <= bounds.BottomRight.Row; row++)
        {
            for (var column = bounds.TopLeft.Column; column <= bounds.BottomRight.Column; column++)
            {
                if (map.RegionAt(new Cell(row, column)) == bounds.Id)
                {
                    RenderingServer.CanvasItemAddRect(item, new Rect2(column * CellSize, row * CellSize, CellSize, CellSize), fill);
                }
            }
        }
    }

    private void DrawEndDots(Rid item)
    {
        var centre = CellCentre(map.End);
        var side = theme.EndDotFraction * CellSize;
        var spacing = theme.EndDotSpacing * CellSize;
        for (var row = -1; row <= 1; row++)
        {
            for (var column = -1; column <= 1; column++)
            {
                var dotCentre = centre + new Vector2(column * spacing, row * spacing);
                dotBox.Draw(item, new Rect2(dotCentre - new Vector2(side, side) / 2, side, side));
            }
        }
    }

    // Two canvas items per block, drawn behind this node's own drawing, so under the start marker, trail and player
    private void CreateBlocks()
    {
        FreeBlocks();
        if (map == null)
        {
            return;
        }
        blocks = new BoardBlocks(map, BlockSize);
        outlinedItems = new Rid[blocks.Count];
        flatItems = new Rid[blocks.Count];
        for (var block = 0; block < blocks.Count; block++)
        {
            outlinedItems[block] = CreateItem(block);
            flatItems[block] = CreateItem(block);
        }
        outlinedBuilt = new bool[blocks.Count];
        flatBuilt = new bool[blocks.Count];
        unbuilt = 2 * blocks.Count;
        ShowVersion();
    }

    // Blocks keep reading order, so a ring is drawn before the regions inside it (BoardBlocks places regions so)
    private Rid CreateItem(int block)
    {
        var item = RenderingServer.CanvasItemCreate();
        RenderingServer.CanvasItemSetParent(item, GetCanvasItem());
        RenderingServer.CanvasItemSetDrawBehindParent(item, true);
        RenderingServer.CanvasItemSetDrawIndex(item, block);
        return item;
    }

    private void ShowVersion()
    {
        if (blocks == null)
        {
            return;
        }
        for (var block = 0; block < blocks.Count; block++)
        {
            RenderingServer.CanvasItemSetVisible(outlinedItems[block], Gap > 0);
            RenderingServer.CanvasItemSetVisible(flatItems[block], Gap == 0);
        }
    }

    // A new theme: every block is rebuilt, the ones on screen in the next UpdateView. Until then they keep their old
    // drawing rather than showing nothing
    private void MarkUnbuilt()
    {
        if (blocks == null)
        {
            return;
        }
        Array.Fill(outlinedBuilt, false);
        Array.Fill(flatBuilt, false);
        unbuilt = 2 * blocks.Count;
        // A theme with too few fills has no colouring (Recolor logged why), so it draws no regions
        if (colors == null)
        {
            foreach (var item in outlinedItems.Concat(flatItems))
            {
                RenderingServer.CanvasItemClear(item);
            }
        }
    }

    private void FreeBlocks()
    {
        if (blocks == null)
        {
            return;
        }
        foreach (var item in outlinedItems.Concat(flatItems))
        {
            RenderingServer.FreeRid(item);
        }
        blocks = null;
        outlinedItems = null;
        flatItems = null;
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

    private StyleBoxFlat RoundedBox(Color fill, int corner)
    {
        var box = new StyleBoxFlat { BgColor = fill, AntiAliasing = true };
        box.AntiAliasingSize /= boxOversampling;
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
