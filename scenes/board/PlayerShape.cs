using Godot;

namespace MergedCells;

// The player's shape from its look, for the board's player and the shop's pictures, so both draw it the same way
public static class PlayerShape
{
    // cell: a square's side in the item's units. flat: the zoomed-out form, where an outline would vanish, so the shape
    // fills in with its own corner and turn
    public static void Draw(CanvasItem item, PlayerLook look, Vector2 centre, float cell, bool flat)
    {
        var side = look.SideFraction * cell;
        var box = new StyleBoxFlat { AntiAliasing = true };
        box.SetCornerRadiusAll(Mathf.RoundToInt(look.CornerFraction * cell));
        if (look.Filled || flat)
        {
            box.BgColor = look.Color;
        }
        else
        {
            box.DrawCenter = false;
            box.BorderColor = look.Color;
            box.SetBorderWidthAll(Mathf.RoundToInt(look.StrokeFraction * cell));
        }
        var turned = look.TurnDegrees != 0;
        if (turned)
        {
            item.DrawSetTransform(centre, Mathf.DegToRad(look.TurnDegrees));
        }
        var origin = turned ? Vector2.Zero : centre;
        item.DrawStyleBox(box, new Rect2(origin.X - side / 2, origin.Y - side / 2, side, side));
        if (turned)
        {
            item.DrawSetTransform(Vector2.Zero);
        }
    }
}
