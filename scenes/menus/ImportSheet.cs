using System;
using Godot;

namespace MergedCells;

// What Import did: a title, the level's row when one was written, a line, Play (only for a level that was written) and Done
public partial class ImportSheet : EditorSheet
{
    private Label title;
    private Container rowSlot;
    private Label line;
    private Button play;

    public event Action PlayPressed;

    public override void _Ready()
    {
        base._Ready();
        title = GetNode<Label>("Sheet/Column/Title");
        rowSlot = GetNode<Container>("Sheet/Column/RowSlot");
        line = GetNode<Label>("Sheet/Column/Line");
        play = GetNode<Button>("Sheet/Column/Play");
        play.Pressed += () => PlayPressed?.Invoke();
        GetNode<Button>("Sheet/Column/Done").Pressed += Close;
    }

    public void Open(string heading, Control row, string text)
    {
        title.Text = heading;
        foreach (var child in rowSlot.GetChildren())
        {
            child.QueueFree();
        }
        if (row is not null)
        {
            rowSlot.AddChild(row);
        }
        rowSlot.Visible = row is not null;
        play.Visible = row is not null;
        line.Text = text;
        Show();
    }
}
