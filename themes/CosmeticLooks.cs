using System.Collections.Generic;
using System.Linq;
using Godot;
using MergedCells.Core;

namespace MergedCells;

// A theme's and look's resources, built from Teal's shipped files by swapping colours, and kept for the run. Teal itself
// is the shipped files, untouched, so the free default can't drift; Teal with the free look is the shipped AppLooks.
// The board and player looks are recoloured by property; the menu theme by colour value (UiRecolour), so every entry a
// later chunk adds to the shipped menu themes recolours itself.
public static class CosmeticLooks
{
    private const string ShippedPath = "res://themes/app_looks.tres";

    private static readonly Dictionary<(string Theme, bool Dark), BoardTheme> Boards = new();
    private static readonly Dictionary<(string Theme, bool Dark), Theme> Menus = new();
    private static readonly Dictionary<(string Theme, string Look, bool Dark), PlayerLook> Players = new();
    private static readonly Dictionary<(string Theme, string Look), AppLooks> All = new();

    private static AppLooks shipped;

    private static AppLooks Shipped => shipped ??= GD.Load<AppLooks>(ShippedPath);

    private static Cosmetics Catalogue => CosmeticsFile.Current;

    public static AppLooks For(CosmeticTheme theme, CosmeticLook look)
    {
        if (theme.Id == Catalogue.DefaultTheme.Id && look.Id == Catalogue.DefaultLook.Id)
        {
            return Shipped;
        }
        if (!All.TryGetValue((theme.Id, look.Id), out var looks))
        {
            looks = new AppLooks
            {
                LightBoard = Board(theme, dark: false),
                DarkBoard = Board(theme, dark: true),
                LightLook = Look(theme, look, dark: false),
                DarkLook = Look(theme, look, dark: true),
                LightUi = Menu(theme, dark: false),
                DarkUi = Menu(theme, dark: true),
            };
            All[(theme.Id, look.Id)] = looks;
        }
        return looks;
    }

    public static BoardTheme Board(CosmeticTheme theme, bool dark)
    {
        if (theme.Id == Catalogue.DefaultTheme.Id)
        {
            return Shipped.Board(dark);
        }
        if (Boards.TryGetValue((theme.Id, dark), out var built))
        {
            return built;
        }
        var palette = theme.For(dark);
        var board = (BoardTheme)Shipped.Board(dark).Duplicate();
        board.Background = Colour(palette.Background);
        board.Outline = Colour(palette.Outline);
        board.Fills = palette.Fills.Select(fill => Colour(fill)).ToArray();
        board.End = Colour(palette.End);
        board.Wall = Colour(palette.Wall);
        board.WallMiddle = Colour(palette.WallMiddle);
        board.Trap = Colour(palette.Trap);
        board.Teleporter = Colour(palette.Teleporter);
        // Sticky squares are unused since chunk 21b; they follow the walls and the lightest fill
        board.Sticky = Colour(palette.Wall);
        board.StickyTint = Colour(palette.Fills[0]);
        board.EditHighlight = Colour(palette.Outline, board.EditHighlight.A);
        board.EraseDim = Colour(palette.Background, board.EraseDim.A);
        Boards[(theme.Id, dark)] = board;
        return board;
    }

    public static PlayerLook Look(CosmeticTheme theme, CosmeticLook look, bool dark)
    {
        if (theme.Id == Catalogue.DefaultTheme.Id && look.Id == Catalogue.DefaultLook.Id)
        {
            return Shipped.Look(dark);
        }
        if (Players.TryGetValue((theme.Id, look.Id, dark), out var built))
        {
            return built;
        }
        var player = theme.For(dark).Player;
        var made = (PlayerLook)Shipped.Look(dark).Duplicate();
        made.Color = Colour(player);
        made.StartOutline = Colour(player, made.StartOutline.A);
        made.SideFraction = (float)look.Side;
        made.StrokeFraction = (float)look.Stroke;
        made.CornerFraction = (float)look.Corner;
        made.Filled = look.Filled;
        made.TurnDegrees = look.Turn;
        Players[(theme.Id, look.Id, dark)] = made;
        return made;
    }

    public static Theme Menu(CosmeticTheme theme, bool dark)
    {
        if (theme.Id == Catalogue.DefaultTheme.Id)
        {
            return Shipped.Ui(dark);
        }
        if (Menus.TryGetValue((theme.Id, dark), out var built))
        {
            return built;
        }
        var from = Catalogue.DefaultTheme.For(dark);
        var to = theme.For(dark);
        var map = UiRecolour.Map(from, to);
        Color Swap(Color colour) => map.TryGetValue(ToRgb(colour), out var target) ? Colour(target, colour.A) : colour;

        // A shallow copy keeps every item, type variation and default, with the styleboxes still shared until swapped
        var menu = (Theme)Shipped.Ui(dark).Duplicate();
        var boxes = new Dictionary<StyleBoxFlat, StyleBoxFlat>();
        var icons = new Dictionary<string, Texture2D>();
        foreach (var type in menu.GetTypeList())
        {
            foreach (var name in menu.GetColorList(type))
            {
                var colour = menu.GetColor(name, type);
                menu.SetColor(name, type, UiRecolour.ChartItems.Contains($"{type}/colors/{name}") ? Colour(to.Chart, colour.A) : Swap(colour));
            }
            foreach (var name in menu.GetStyleboxList(type))
            {
                if (menu.GetStylebox(name, type) is not StyleBoxFlat flat)
                {
                    continue;
                }
                if (!boxes.TryGetValue(flat, out var copy))
                {
                    copy = (StyleBoxFlat)flat.Duplicate();
                    copy.BgColor = Swap(flat.BgColor);
                    copy.BorderColor = Swap(flat.BorderColor);
                    copy.ShadowColor = Swap(flat.ShadowColor);
                    boxes[flat] = copy;
                }
                menu.SetStylebox(name, type, copy);
            }
            foreach (var name in menu.GetIconList(type))
            {
                if (BakedIcon(menu.GetIcon(name, type).ResourcePath, to, icons) is { } icon)
                {
                    menu.SetIcon(name, type, icon);
                }
            }
        }
        Menus[(theme.Id, dark)] = menu;
        return menu;
    }

    // The three icons with colours baked in, remade in a theme's colours; every other icon is white and takes its colour
    // from the theme
    private static Texture2D BakedIcon(string path, Palette palette, Dictionary<string, Texture2D> made)
    {
        var file = path.GetFile();
        if (BakedIcons.For(file, palette) is not { } svg)
        {
            return null;
        }
        if (!made.TryGetValue(file, out var texture))
        {
            var image = new Image();
            image.LoadSvgFromString(svg);
            texture = ImageTexture.CreateFromImage(image);
            made[file] = texture;
        }
        return texture;
    }

    private static Color Colour(Rgb colour, float alpha = 1) => new(colour.R / 255f, colour.G / 255f, colour.B / 255f, alpha);

    private static Rgb ToRgb(Color colour) =>
        new((byte)Mathf.RoundToInt(colour.R * 255), (byte)Mathf.RoundToInt(colour.G * 255), (byte)Mathf.RoundToInt(colour.B * 255));
}
