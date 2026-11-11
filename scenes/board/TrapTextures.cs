using Godot;
using MergedCells.Core;

namespace MergedCells;

// A trap never joins its neighbours, so one texture per form covers every trap on a map
internal sealed class TrapTextures
{
    // Pixels per square, as for the wall and sticky textures
    private const int OutlinedSide = 256;
    private const int FlatSide = 64;

    private readonly Color color;
    private readonly float size;
    private readonly float stroke;
    private readonly float flatSize;
    private readonly float flatStroke;
    private ImageTexture outlined;
    private ImageTexture flat;

    public TrapTextures(BoardTheme theme)
    {
        color = theme.Trap;
        size = theme.TrapSizeFraction;
        stroke = theme.TrapStrokeFraction;
        flatSize = theme.TrapFlatSizeFraction;
        flatStroke = theme.TrapFlatStrokeFraction;
    }

    public Texture2D For(bool outlinedForm) =>
        outlinedForm
            ? outlined ??= Bake(OutlinedSide, size, stroke)
            : flat ??= Bake(FlatSide, flatSize, flatStroke);

    private ImageTexture Bake(int side, float crossSize, float width)
    {
        var bytes = new byte[side * side * 4];
        for (var row = 0; row < side; row++)
        {
            for (var column = 0; column < side; column++)
            {
                var x = (column + 0.5f) / side;
                var y = (row + 0.5f) / side;
                // The edge blends over one pixel. Clear pixels keep the colour, so filtering leaves no dark fringe
                var alpha = Mathf.Clamp(TrapCross.Distance(x, y, crossSize, width) * side + 0.5f, 0, 1);
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
}
