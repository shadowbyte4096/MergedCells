using System;
using System.Collections.Generic;
using Godot;
using MergedCells.Core;

namespace MergedCells;

// A wall square's rings depend only on which neighbours are walls, so one texture per neighbour pattern, baked on first
// use, covers every wall on a map. Each pattern has an outlined form (inset, rounded) and a flat, zoomed-out one.
internal sealed class WallTextures
{
    // Pixels per square: drawing details like BoardView.BlockSize, not style. The outlined side keeps the ring steps
    // sharp at the closest zoom; the flat form is only shown small
    private const int OutlinedSide = 256;
    private const int FlatSide = 64;

    private readonly Dictionary<(WallSides Walls, bool Outlined), ImageTexture> textures = new();
    private readonly Color[] ringColors;
    private readonly float inset;
    private readonly float corner;

    public WallTextures(BoardTheme theme)
    {
        var rings = Math.Max(theme.WallRings, 1);
        ringColors = new Color[rings];
        for (var ring = 0; ring < rings; ring++)
        {
            ringColors[ring] = rings == 1 ? theme.Wall : theme.Wall.Lerp(theme.WallMiddle, ring / (rings - 1f));
        }
        inset = theme.GapFraction / 2;
        // Rounded to whole board units and capped as the blocks' corners are
        corner = Mathf.Min(Mathf.RoundToInt(theme.CornerFraction * BoardView.CellSize) / BoardView.CellSize, 0.5f - inset);
    }

    public Texture2D For(WallSides walls, bool outlined)
    {
        if (!textures.TryGetValue((walls, outlined), out var texture))
        {
            texture = Bake(walls, outlined);
            textures.Add((walls, outlined), texture);
        }
        return texture;
    }

    private ImageTexture Bake(WallSides walls, bool outlined)
    {
        var side = outlined ? OutlinedSide : FlatSide;
        var ringInset = outlined ? inset : 0;
        var ringCorner = outlined ? corner : 0;
        var edge = ringColors[0];
        var bytes = new byte[side * side * 4];
        for (var row = 0; row < side; row++)
        {
            for (var column = 0; column < side; column++)
            {
                var x = (column + 0.5f) / side;
                var y = (row + 0.5f) / side;
                // Outside the wall the pixel is clear but keeps the edge colour, so filtering leaves no dark fringe
                var color = edge;
                var alpha = Coverage(walls, x, y, ringInset, ringCorner, 0, side);
                for (var ring = 1; ring < ringColors.Length && alpha > 0; ring++)
                {
                    var coverage = Coverage(walls, x, y, ringInset, ringCorner, ring, side);
                    if (coverage <= 0)
                    {
                        break;
                    }
                    color = color.Lerp(ringColors[ring], coverage);
                }
                var index = (row * side + column) * 4;
                bytes[index] = (byte)color.R8;
                bytes[index + 1] = (byte)color.G8;
                bytes[index + 2] = (byte)color.B8;
                bytes[index + 3] = (byte)Mathf.RoundToInt(alpha * 255);
            }
        }
        var image = Image.CreateFromData(side, side, false, Image.Format.Rgba8, bytes);
        image.GenerateMipmaps();
        return ImageTexture.CreateFromImage(image);
    }

    // How much of the pixel lies inside the ring: the ring's edge blends over one pixel
    private float Coverage(WallSides walls, float x, float y, float ringInset, float ringCorner, int ring, int side) =>
        Mathf.Clamp(WallRings.Distance(walls, x, y, ringInset, ringCorner, ring, ringColors.Length) * side + 0.5f, 0, 1);
}
