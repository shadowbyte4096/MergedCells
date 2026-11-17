using System.Collections.Generic;
using Godot;

namespace MergedCells;

// The campaign levels the player has 3-starred, by chapter, to copy into the editor. A tall sheet over the list; the
// screen builds the headings and rows, which pass presses to the scroll as the list's rows do
public partial class CopySheet : EditorSheet
{
    private VBoxContainer list;
    private ScrollContainer scroll;

    public override void _Ready()
    {
        base._Ready();
        list = GetNode<VBoxContainer>("Sheet/Column/Scroll/List");
        scroll = GetNode<ScrollContainer>("Sheet/Column/Scroll");
    }

    public void Open(IEnumerable<Control> items)
    {
        foreach (var child in list.GetChildren())
        {
            child.QueueFree();
        }
        foreach (var item in items)
        {
            list.AddChild(item);
        }
        scroll.ScrollVertical = 0;
        Show();
    }
}
