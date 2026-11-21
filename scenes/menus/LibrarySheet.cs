using System;
using Godot;
using MergedCells.Core;

namespace MergedCells;

// One library level: its picture, name, size, par and band, and how many have beaten and liked it. Browsing another
// player's level: Get level, then Report and Block this player as separate buttons (Google Play asks for both, clearly
// labelled). The player's own: Remove from library. Block and Remove ask for a second tap
public partial class LibrarySheet : EditorSheet
{
    private Container pictureSlot;
    private Label title;
    private Label line;
    private Label stats;
    private Button get;
    private Control rule;
    private Control pair;
    private Button report;
    private Button block;
    private Button remove;
    private Label note;
    private bool blockArmed;
    private bool removeArmed;
    private Func<string> waiting;

    public event Action GetPressed;

    public event Action ReportPressed;

    public event Action BlockConfirmed;

    public event Action RemoveConfirmed;

    // A block or remove has its answer, so the screen can load the list again
    public event Action Settled;

    public override void _Ready()
    {
        base._Ready();
        pictureSlot = GetNode<Container>("Sheet/Column/PictureSlot");
        title = GetNode<Label>("Sheet/Column/Title");
        line = GetNode<Label>("Sheet/Column/Line");
        stats = GetNode<Label>("Sheet/Column/Stats");
        get = GetNode<Button>("Sheet/Column/Get");
        rule = GetNode<Control>("Sheet/Column/Rule");
        pair = GetNode<Control>("Sheet/Column/Pair");
        report = GetNode<Button>("Sheet/Column/Pair/Report");
        block = GetNode<Button>("Sheet/Column/Pair/Block");
        remove = GetNode<Button>("Sheet/Column/Remove");
        note = GetNode<Label>("Sheet/Column/Note");
        get.Pressed += () => GetPressed?.Invoke();
        report.Pressed += () =>
        {
            Disarm();
            ReportPressed?.Invoke();
        };
        block.Pressed += OnBlockPressed;
        remove.Pressed += OnRemovePressed;
    }

    // picture: the level's picture, or null for a tombstone the phone doesn't hold
    public void Open(LibraryEntry entry, Control picture, string name, string metaLine, string statsLine, LibraryGet getState)
    {
        foreach (var child in pictureSlot.GetChildren())
        {
            child.QueueFree();
        }
        if (picture is not null)
        {
            pictureSlot.AddChild(picture);
        }
        pictureSlot.Visible = picture is not null;
        title.Text = name;
        line.Text = metaLine;
        line.Visible = metaLine.Length > 0;
        stats.Text = statsLine;
        get.Visible = entry.Level is not null;
        get.Disabled = getState == LibraryGet.Have;
        get.Text = getState switch
        {
            LibraryGet.Newer => "Get the newer version",
            LibraryGet.Have => "You have this level",
            _ => "Get level",
        };
        var others = !entry.Mine;
        rule.Visible = others || !entry.Removed;
        pair.Visible = others;
        remove.Visible = entry.Mine && !entry.Removed;
        report.Disabled = false;
        block.Disabled = false;
        remove.Disabled = false;
        note.Hide();
        waiting = null;
        Disarm();
        Show();
    }

    // Shows the sentence for a call under way and polls outcome each frame until it gives the outcome's; the buttons
    // ignore presses meanwhile, so a double tap sends one call
    public void Watch(string pending, Func<string> outcome)
    {
        note.Text = pending;
        note.Show();
        waiting = outcome;
        block.Disabled = true;
        remove.Disabled = true;
    }

    public override void _Process(double delta)
    {
        if (waiting?.Invoke() is not { } sentence)
        {
            return;
        }
        waiting = null;
        note.Text = sentence;
        Settled?.Invoke();
    }

    private void OnBlockPressed()
    {
        if (waiting is not null)
        {
            return;
        }
        if (!blockArmed)
        {
            blockArmed = true;
            block.Text = "Tap again to block";
            block.ThemeTypeVariation = "PillPrimary";
            return;
        }
        Disarm();
        BlockConfirmed?.Invoke();
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

    private void Disarm()
    {
        blockArmed = false;
        block.Text = "Block this player";
        block.ThemeTypeVariation = "PillButton";
        removeArmed = false;
        remove.Text = "Remove from library";
    }
}
