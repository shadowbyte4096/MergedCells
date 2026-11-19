using System;
using Godot;

namespace MergedCells;

// One custom level's actions: Play and Share code (only when it can be played), Edit (Edit a copy for an imported level),
// Edit a copy as well for the player's own level, and Delete, which asks "Tap again to delete" first
public partial class LevelSheet : EditorSheet
{
    private Label title;
    private Label line;
    private Button play;
    private Button share;
    private Label note;
    private Button edit;
    private Button editCopy;
    private Button delete;
    private bool deleteArmed;

    public event Action PlayPressed;

    public event Action SharePressed;

    public event Action EditPressed;

    public event Action EditCopyPressed;

    public event Action DeleteConfirmed;

    public override void _Ready()
    {
        base._Ready();
        title = GetNode<Label>("Sheet/Column/Title");
        line = GetNode<Label>("Sheet/Column/Line");
        play = GetNode<Button>("Sheet/Column/Play");
        share = GetNode<Button>("Sheet/Column/Share");
        note = GetNode<Label>("Sheet/Column/Note");
        edit = GetNode<Button>("Sheet/Column/Edit");
        editCopy = GetNode<Button>("Sheet/Column/EditCopy");
        delete = GetNode<Button>("Sheet/Column/Delete");
        play.Pressed += () => PlayPressed?.Invoke();
        share.Pressed += () => SharePressed?.Invoke();
        edit.Pressed += () => EditPressed?.Invoke();
        editCopy.Pressed += () => EditCopyPressed?.Invoke();
        delete.Pressed += OnDeletePressed;
    }

    // shareable: playable, and holdable by a share code (a hand-edited file may not be)
    public void Open(string name, string status, bool playable, bool shareable, bool imported)
    {
        title.Text = name;
        line.Text = imported ? $"Imported · {status}" : status;
        play.Disabled = !playable;
        share.Disabled = !shareable;
        note.Hide();
        edit.Text = imported ? "Edit a copy" : "Edit";
        editCopy.Visible = !imported;
        deleteArmed = false;
        delete.Text = "Delete";
        Show();
    }

    public void ShowCopied()
    {
        note.Text = "Code copied. Paste it to a friend.";
        note.Show();
    }

    private void OnDeletePressed()
    {
        if (!deleteArmed)
        {
            deleteArmed = true;
            delete.Text = "Tap again to delete";
            return;
        }
        DeleteConfirmed?.Invoke();
    }
}
