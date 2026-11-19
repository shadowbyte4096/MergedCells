using System;
using Godot;

namespace MergedCells;

// A pill that fills with ink from the left while a map is made, its text in ink over the empty part and in the card colour
// over the fill. Its looks are the UI theme's MakingBar entries
public partial class MakingBar : Control
{
    private const string Type = "MakingBar";

    // The fill and its text are drawn at the bar's full size inside a box as wide as the progress, which clips them
    private readonly Control clip = new() { ClipContents = true, MouseFilter = MouseFilterEnum.Ignore };
    private readonly Control filled = new() { MouseFilter = MouseFilterEnum.Ignore };
    private double progress;
    private string text = "";

    public double Progress
    {
        set
        {
            progress = Math.Clamp(value, 0, 1);
            Layout();
        }
    }

    public string Text
    {
        set
        {
            text = value;
            QueueRedraw();
            filled.QueueRedraw();
        }
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        clip.AddChild(filled);
        AddChild(clip);
        filled.Draw += DrawFilled;
        Resized += Layout;
        Layout();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationThemeChanged)
        {
            QueueRedraw();
            filled.QueueRedraw();
        }
    }

    public override void _Draw()
    {
        DrawStyleBox(GetThemeStylebox("track", Type), new Rect2(Vector2.Zero, Size));
        DrawText(this, GetThemeColor("font_color", Type));
    }

    private void DrawFilled()
    {
        filled.DrawStyleBox(GetThemeStylebox("fill", Type), new Rect2(Vector2.Zero, Size));
        DrawText(filled, GetThemeColor("font_fill_color", Type));
    }

    private void DrawText(CanvasItem on, Color color)
    {
        var font = GetThemeFont("font", Type);
        var fontSize = GetThemeFontSize("font_size", Type);
        var width = font.GetStringSize(text, HorizontalAlignment.Left, -1, fontSize).X;
        var baseline = (Size.Y - font.GetHeight(fontSize)) / 2 + font.GetAscent(fontSize);
        on.DrawString(font, new Vector2((Size.X - width) / 2, baseline), text, HorizontalAlignment.Left, -1, fontSize, color);
    }

    private void Layout()
    {
        filled.Size = Size;
        clip.Size = new Vector2((float)(Size.X * progress), Size.Y);
        filled.QueueRedraw();
    }
}
