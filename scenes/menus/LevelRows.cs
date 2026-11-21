using System;
using System.Collections.Generic;
using Godot;
using MergedCells.Core;

namespace MergedCells;

// How the custom levels list and the library draw a level's row, so both look alike
public static class LevelRows
{
    // A level's picture (or none, for a level the phone can't show), its name with an optional tag, a line under it and
    // an optional third line, in a row panel. The tag sits at the end of the name's line: Godot's HBox can't keep it
    // hugging a name that has to shrink and ellipsize
    public static PanelContainer Panel(
        string variation, Map map, BoardTheme board, List<LevelPictureView> pictures, string name, string tag, string line, bool warn,
        string stats = null, bool solidTag = false)
    {
        var panel = new PanelContainer { ThemeTypeVariation = variation, MouseFilter = Control.MouseFilterEnum.Ignore };
        var content = new HBoxContainer { ThemeTypeVariation = "PictureRow", MouseFilter = Control.MouseFilterEnum.Ignore };
        if (map is not null)
        {
            var picture = new LevelPictureView
            {
                Map = map,
                Board = board,
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            };
            pictures.Add(picture);
            content.AddChild(picture);
        }
        var column = new VBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        var nameRow = new HBoxContainer { ThemeTypeVariation = "NameRow", MouseFilter = Control.MouseFilterEnum.Ignore };
        nameRow.AddChild(new Label
        {
            Text = name,
            ThemeTypeVariation = "RowName",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            ClipText = true,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
        });
        if (tag is not null)
        {
            nameRow.AddChild(new Label
            {
                Text = tag,
                ThemeTypeVariation = solidTag ? "YoursTag" : "ImportedTag",
                MouseFilter = Control.MouseFilterEnum.Ignore,
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            });
        }
        column.AddChild(nameRow);
        column.AddChild(Line(line, warn ? "RowMetaWarn" : "RowMeta"));
        if (stats is not null)
        {
            column.AddChild(Line(stats, "RowStats"));
        }
        content.AddChild(column);
        panel.AddChild(content);
        return panel;
    }

    // A see-through button over the whole row takes the tap. It passes presses on, so the scroll container can drag, and
    // ignores a press at the end of a drag (dragged: whether the touch has moved past the swipe distance)
    public static MarginContainer Pressable(Control panel, Action pressed, Func<bool> dragged)
    {
        var row = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddChild(panel);
        var button = new Button
        {
            ThemeTypeVariation = "ListRow",
            FocusMode = Control.FocusModeEnum.None,
            MouseFilter = Control.MouseFilterEnum.Pass,
        };
        button.Pressed += () =>
        {
            if (!dragged())
            {
                pressed();
            }
        };
        row.AddChild(button);
        return row;
    }

    private static Label Line(string text, string variation) => new()
    {
        Text = text,
        ThemeTypeVariation = variation,
        MouseFilter = Control.MouseFilterEnum.Ignore,
        ClipText = true,
        TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
    };
}
