using System;
using System.Collections.Generic;
using Godot;
using MergedCells.Core;

namespace MergedCells;

// A sticky square's shape depends only on which neighbours it joins, so one texture per pattern, baked on first use,
// covers every sticky square on a map. The flat, zoomed-out form fills the square, so it has one texture for all.
internal sealed class StickyTextures
{
    // Pixels per square, as for the wall textures
    private const int OutlinedSide = 256;
    private const int FlatSide = 64;

    private readonly Dictionary<(WallSides Joined, bool Outlined), ImageTexture> textures = new();
    private readonly Color stripe;
    private readonly Color tint;
    private readonly int stripes;
    private readonly float stripeWidth;
    private readonly float inset;
    private readonly float corner;

    public StickyTextures(BoardTheme theme)
    {
        stripe = theme.Sticky;
        tint = theme.StickyTint;
        stripes = Math.Max(theme.StickyStripes, 1);
        stripeWidth = theme.StickyStripeFraction;
        inset = Mathf.Clamp(theme.StickyInsetFraction, 0, 0.5f);
        corner = Mathf.Clamp(theme.StickyCornerFraction, 0, 0.5f - inset);
    }

    public Texture2D For(WallSides joined, bool outlined)
    {
        var key = (outlined ? joined : WallSides.None, outlined);
        if (!textures.TryGetValue(key, out var texture))
        {
            texture = Bake(key.Item1, outlined);
            textures.Add(key, texture);
        }
        return texture;
    }

    private ImageTexture Bake(WallSides joined, bool outlined)
    {
        var side = outlined ? OutlinedSide : FlatSide;
        var shapeInset = outlined ? inset : 0;
        var shapeCorner = outlined ? corner : 0;
        var bytes = new byte[side * side * 4];
        for (var row = 0; row < side; row++)
        {
            for (var column = 0; column < side; column++)
            {
                var x = (column + 0.5f) / side;
                var y = (row + 0.5f) / side;
                // Edges blend over one pixel. Clear pixels keep their colour, so filtering leaves no dark fringe
                var color = tint.Lerp(stripe, Coverage(StickyStripes.Distance(x, y, stripes, stripeWidth), side));
                var alpha = Coverage(WallRings.Distance(joined, x, y, shapeInset, shapeCorner, 0, 1), side);
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

    private static float Coverage(float distance, int side) => Mathf.Clamp(distance * side + 0.5f, 0, 1);
}
