using System;
using Godot;
using MergedCells.Core;

namespace MergedCells;

// "Everyone's best moves" on the results card: dashed empty columns while the server is asked, then the bars, each
// column's share, the axis in moves and the line in words; or "you're the first", or "can't reach". One height in every
// state, so the card never moves. Looks are the UI theme's BestMovesChart entries
public partial class BestMovesChart : Control
{
    private const string Type = "BestMovesChart";
    private const string Title = "Everyone's best moves";
    private const string FirstLine = "You're the first to finish this level.";
    private const string FirstDetail = "Others' best moves show here as they finish it.";
    private const string Unreachable = "Can't reach the leaderboards right now. Your best is saved and goes up next time you're online.";
    // Without "your best is saved": the server is off for the run because the save can't keep anything
    private const string UnreachableOff = "Can't reach the leaderboards right now.";

    private LeaderboardSource source;
    private BestMovesSpread spread;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetProcess(false);
        Fit();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationThemeChanged)
        {
            Fit();
            QueueRedraw();
        }
    }

    // A new card's source, or null when the card hides; a fetch still waiting for the card it replaces is never sent
    public void Watch(LeaderboardSource next)
    {
        if (source is not null && source != next)
        {
            source.Fetch.Cancel();
        }
        source = next;
        spread = null;
        SetProcess(next is not null);
        Poll();
        QueueRedraw();
    }

    // Leaving the play screen (Menu, Back) before the answer
    public override void _ExitTree() => source?.Fetch.Cancel();

    // Polled rather than subscribed: the card can go before the answer, and an event would then call into a freed node
    public override void _Process(double delta)
    {
        if (IsVisibleInTree())
        {
            Poll();
        }
    }

    private void Poll()
    {
        if (source is null || source.Fetch.State == SpreadFetchState.Waiting)
        {
            return;
        }
        if (source.Fetch.Counts is { } counts)
        {
            spread = BestMovesSpread.Of(counts, source.Axis);
        }
        SetProcess(false);
        QueueRedraw();
    }

    private void Fit() => CustomMinimumSize = new Vector2(0, GetThemeConstant("height", Type));

    public override void _Draw()
    {
        if (source is null)
        {
            return;
        }
        var state = source.Fetch.State;
        var count = spread is { Players: > 0 } ? spread.PlayersText : null;
        var top = DrawTitle(count);
        if (state is SpreadFetchState.Unavailable or SpreadFetchState.Off)
        {
            DrawMessage(top, null, state == SpreadFetchState.Off ? UnreachableOff : Unreachable);
        }
        else if (spread is { IsFirst: true })
        {
            DrawMessage(top, FirstLine, FirstDetail);
        }
        else
        {
            DrawChart(top);
        }
    }

    // The title, with the player count once known; returns where the rest starts
    private float DrawTitle(string count)
    {
        var bold = GetThemeFont("bold_font", Type);
        var font = GetThemeFont("font", Type);
        var size = GetThemeFontSize("title_size", Type);
        var rest = count is null ? "" : $" · {count}";
        var titleWidth = bold.GetStringSize(Title, HorizontalAlignment.Left, -1, size).X;
        var x = (Size.X - titleWidth - font.GetStringSize(rest, HorizontalAlignment.Left, -1, size).X) / 2;
        var baseline = bold.GetAscent(size);
        DrawString(bold, new Vector2(x, baseline), Title, HorizontalAlignment.Left, -1, size, GetThemeColor("ink", Type));
        DrawString(font, new Vector2(x + titleWidth, baseline), rest, HorizontalAlignment.Left, -1, size, GetThemeColor("muted", Type));
        return bold.GetHeight(size) + GetThemeConstant("spacing", Type);
    }

    // Centred in the space under the title: an optional bold line, then muted text, both wrapped to the width
    private void DrawMessage(float top, string line, string detail)
    {
        var bold = GetThemeFont("bold_font", Type);
        var font = GetThemeFont("font", Type);
        var size = GetThemeFontSize("state_size", Type);
        var spacing = GetThemeConstant("spacing", Type);
        var lineHeight = line is null ? 0 : bold.GetMultilineStringSize(line, HorizontalAlignment.Center, Size.X, size).Y + spacing;
        var detailHeight = font.GetMultilineStringSize(detail, HorizontalAlignment.Center, Size.X, size).Y;
        var y = top + (Size.Y - top - lineHeight - detailHeight) / 2;
        if (line is not null)
        {
            DrawMultilineString(bold, new Vector2(0, y + bold.GetAscent(size)), line, HorizontalAlignment.Center, Size.X, size, -1, GetThemeColor("ink", Type));
            y += lineHeight;
        }
        DrawMultilineString(font, new Vector2(0, y + font.GetAscent(size)), detail, HorizontalAlignment.Center, Size.X, size, -1, GetThemeColor("muted", Type));
    }

    private void DrawChart(float top)
    {
        var axis = source.Axis;
        var bold = GetThemeFont("bold_font", Type);
        var font = GetThemeFont("font", Type);
        var ink = GetThemeColor("ink", Type);
        var muted = GetThemeColor("muted", Type);
        var shareSize = GetThemeFontSize("share_size", Type);
        var labelSize = GetThemeFontSize("label_size", Type);
        var subSize = GetThemeFontSize("sub_size", Type);
        var lineSize = GetThemeFontSize("line_size", Type);
        var gap = GetThemeConstant("gap", Type);
        var spacing = GetThemeConstant("spacing", Type);
        var floor = GetThemeConstant("floor", Type);

        // From the bottom up: the line, the small "par" and "you", the move counts; the bars take what's left
        var lineTop = Size.Y - bold.GetHeight(lineSize);
        var subTop = lineTop - spacing - font.GetHeight(subSize);
        var labelTop = subTop - bold.GetHeight(labelSize);
        var barsBottom = labelTop - spacing;
        var barsTop = top + bold.GetHeight(shareSize);
        var barsHeight = barsBottom - barsTop;
        var width = (Size.X - gap * (axis.Columns - 1)) / axis.Columns;
        var ghostHeight = barsHeight * GetThemeConstant("ghost_percent", Type) / 100f;

        for (var column = 0; column < axis.Columns; column++)
        {
            var left = column * (width + gap);
            var you = column == axis.BestColumn;
            if (spread is null)
            {
                DrawGhost(new Rect2(left, barsBottom - ghostHeight, width, ghostHeight));
            }
            else
            {
                var bar = spread.Columns[column];
                var height = Math.Max(floor, bar.Height * barsHeight);
                DrawStyleBox(GetThemeStylebox(you ? "bar_you" : "bar", Type), new Rect2(left, barsBottom - height, width, height));
                var shareFont = you ? bold : font;
                DrawCentred(shareFont, bar.Share, left + width / 2, barsBottom - height - spacing / 2f, shareSize, you ? ink : muted);
            }
            var labelFont = you ? bold : font;
            DrawCentred(labelFont, axis.Label(column), left + width / 2, labelTop + labelFont.GetAscent(labelSize), labelSize, you ? ink : muted);
            var sub = column == 0 && you ? "par · you" : column == 0 ? "par" : you ? "you" : null;
            if (sub is not null)
            {
                DrawCentred(font, sub, left + width / 2, subTop + font.GetAscent(subSize), subSize, you ? ink : muted);
            }
        }
        if (spread?.Line is { } line)
        {
            DrawString(bold, new Vector2(0, lineTop + bold.GetAscent(lineSize)), line, HorizontalAlignment.Center, Size.X, lineSize, ink);
        }
    }

    // Centred on its column but never cut to the column's width (a phone's columns are too narrow for "par · you"),
    // kept inside the chart
    private void DrawCentred(Font font, string text, float centre, float baseline, int size, Color color)
    {
        var width = font.GetStringSize(text, HorizontalAlignment.Left, -1, size).X;
        var x = Math.Clamp(centre - width / 2, 0, Math.Max(0, Size.X - width));
        DrawString(font, new Vector2(x, baseline), text, HorizontalAlignment.Left, -1, size, color);
    }

    // A dashed outline, open at the floor, standing in for a bar until the answer comes
    private void DrawGhost(Rect2 rect)
    {
        var color = GetThemeColor("ghost", Type);
        var dash = GetThemeConstant("dash", Type);
        var line = GetThemeConstant("dash_width", Type);
        var topLeft = rect.Position;
        var topRight = new Vector2(rect.End.X, rect.Position.Y);
        DrawDashedLine(new Vector2(rect.Position.X, rect.End.Y), topLeft, color, line, dash);
        DrawDashedLine(topLeft, topRight, color, line, dash);
        DrawDashedLine(topRight, rect.End, color, line, dash);
    }
}
