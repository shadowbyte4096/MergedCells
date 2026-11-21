using System;
using Godot;
using MergedCells.Core;

namespace MergedCells;

// One custom level's actions: Play and Share code (only when it can be played), for the player's own level Publish to
// library (or Publish update) and Remove from library, Edit (Edit a copy for an imported level), Edit a copy as well for
// the player's own level, and Delete, which asks "Tap again to delete" first
public partial class LevelSheet : EditorSheet
{
    private Label title;
    private Label line;
    private Button play;
    private Button share;
    private Control publishRow;
    private Button publish;
    private Button remove;
    private Label note;
    private Button edit;
    private Button editCopy;
    private Button delete;
    private bool deleteArmed;
    private bool removeArmed;
    private bool published;

    // A call the sheet is waiting on: null while it waits, then the sentence that says how it went
    private Func<string> waiting;

    public event Action PlayPressed;

    public event Action SharePressed;

    public event Action PublishPressed;

    public event Action RemoveConfirmed;

    public event Action EditPressed;

    public event Action EditCopyPressed;

    public event Action DeleteConfirmed;

    // A publish or remove has its answer, so the screen can work the buttons out again
    public event Action Settled;

    public override void _Ready()
    {
        base._Ready();
        title = GetNode<Label>("Sheet/Column/Title");
        line = GetNode<Label>("Sheet/Column/Line");
        play = GetNode<Button>("Sheet/Column/Play");
        share = GetNode<Button>("Sheet/Column/Share");
        publishRow = GetNode<Control>("Sheet/Column/PublishRow");
        publish = GetNode<Button>("Sheet/Column/PublishRow/Publish");
        remove = GetNode<Button>("Sheet/Column/PublishRow/Remove");
        note = GetNode<Label>("Sheet/Column/Note");
        edit = GetNode<Button>("Sheet/Column/Edit");
        editCopy = GetNode<Button>("Sheet/Column/EditCopy");
        delete = GetNode<Button>("Sheet/Column/Delete");
        play.Pressed += () => PlayPressed?.Invoke();
        share.Pressed += () => SharePressed?.Invoke();
        publish.Pressed += () =>
        {
            if (waiting is not null)
            {
                return;
            }
            Disarm();
            PublishPressed?.Invoke();
        };
        remove.Pressed += OnRemovePressed;
        edit.Pressed += () => EditPressed?.Invoke();
        editCopy.Pressed += () => EditCopyPressed?.Invoke();
        delete.Pressed += OnDeletePressed;
    }

    // shareable: playable, and holdable by a share code (a hand-edited file may not be). publish: null for an imported
    // level, which has no publish row; published: this phone published it, so Remove shows
    public void Open(string name, string status, bool playable, bool shareable, bool imported, PublishState? publish, bool published)
    {
        title.Text = name;
        line.Text = imported ? $"Imported · {status}" : status;
        play.Disabled = !playable;
        share.Disabled = !shareable;
        note.Hide();
        edit.Text = imported ? "Edit a copy" : "Edit";
        editCopy.Visible = !imported;
        waiting = null;
        SetPublish(publish, published);
        Show();
    }

    // The publish row for the level as it is now, keeping any note already showing
    public void SetPublish(PublishState? state, bool published)
    {
        this.published = published;
        Disarm();
        publishRow.Visible = state is not null;
        if (state is not { } current)
        {
            return;
        }
        publish.Visible = current != PublishState.UpToDate;
        publish.Text = current == PublishState.Update ? "Publish update" : "Publish to library";
        publish.Disabled = current is PublishState.NotPlayable or PublishState.TooBig or PublishState.Unavailable;
        remove.Visible = published;
        remove.Disabled = current == PublishState.Unavailable;
        var reason = current switch
        {
            PublishState.TooBig => "This level is too big for the library.",
            PublishState.Unavailable => "The library isn't available right now.",
            _ => null,
        };
        if (reason is not null && !note.Visible)
        {
            ShowNote(reason);
        }
    }

    public void ShowCopied() => ShowNote("Code copied. Paste it to a friend.");

    // Shows the sentence for a call under way and polls line each frame until it gives the outcome's; the publish row
    // ignores presses meanwhile, so a double tap sends one call
    public void Watch(string pending, Func<string> outcome)
    {
        ShowNote(pending);
        waiting = outcome;
        publish.Disabled = true;
        remove.Disabled = true;
    }

    public override void _Process(double delta)
    {
        if (waiting?.Invoke() is not { } sentence)
        {
            return;
        }
        waiting = null;
        ShowNote(sentence);
        Settled?.Invoke();
    }

    private void ShowNote(string sentence)
    {
        note.Text = sentence;
        note.Show();
    }

    private void OnRemovePressed()
    {
        if (waiting is not null)
        {
            return;
        }
        if (!removeArmed)
        {
            removeArmed = true;
            remove.Text = "Tap again to remove";
            return;
        }
        Disarm();
        RemoveConfirmed?.Invoke();
    }

    private void OnDeletePressed()
    {
        if (!deleteArmed)
        {
            deleteArmed = true;
            delete.Text = published ? "Tap again to delete. It stays in the library." : "Tap again to delete";
            return;
        }
        DeleteConfirmed?.Invoke();
    }

    private void Disarm()
    {
        deleteArmed = false;
        delete.Text = "Delete";
        removeArmed = false;
        remove.Text = "Remove from library";
    }
}
