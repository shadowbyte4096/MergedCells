using System;
using Godot;
using MergedCells.Core;

namespace MergedCells;

// Reporting a library level: three reasons, and a tap on one sends the report. A level several players report is hidden
// until the developer checks it
public partial class ReportSheet : EditorSheet
{
    private Label title;
    private Button name;
    private Button picture;
    private Button other;
    private Button done;
    private Label note;
    private Func<string> waiting;

    public event Action<ReportReason> Chosen;

    public override void _Ready()
    {
        base._Ready();
        title = GetNode<Label>("Sheet/Column/Title");
        name = GetNode<Button>("Sheet/Column/Name");
        picture = GetNode<Button>("Sheet/Column/Picture");
        other = GetNode<Button>("Sheet/Column/Other");
        done = GetNode<Button>("Sheet/Column/Done");
        note = GetNode<Label>("Sheet/Column/Note");
        name.Pressed += () => Choose(ReportReason.Name);
        picture.Pressed += () => Choose(ReportReason.Picture);
        other.Pressed += () => Choose(ReportReason.Other);
        done.Pressed += Close;
    }

    public void Open(string levelName)
    {
        title.Text = $"Report {levelName}";
        SetReasons(enabled: true);
        done.Text = "Cancel";
        note.Hide();
        waiting = null;
        Show();
    }

    // The report under way: "Sending…", then how it went, polled each frame
    public void Watch(Func<string> outcome)
    {
        note.Text = "Sending…";
        note.Show();
        waiting = outcome;
    }

    public override void _Process(double delta)
    {
        if (waiting?.Invoke() is not { } sentence)
        {
            return;
        }
        waiting = null;
        note.Text = sentence;
        done.Text = "Done";
    }

    // One report per tap: the reasons grey out once one is sent
    private void Choose(ReportReason reason)
    {
        SetReasons(enabled: false);
        Chosen?.Invoke(reason);
    }

    private void SetReasons(bool enabled)
    {
        name.Disabled = !enabled;
        picture.Disabled = !enabled;
        other.Disabled = !enabled;
    }
}
