using System;
using Godot;
using MergedCells.Core;

namespace MergedCells;

// The one-time ask over the mode select: one kind of data at a time, its wording, the policy link and two answers of equal
// weight. A tap on the wash, Esc or Back closes it unanswered.
public partial class ConsentSheet : Control
{
    private Label title;
    private Label lead;
    private Label body;
    private Label small;
    private Button no;
    private Button yes;
    private ConsentWording asking;
    private ulong askedAt;

    public event Action<ConsentWording, bool> Answered;

    // A press this soon after an ask appears is ignored, so a double tap on one ask can't answer the next unread
    public double AnswerGuardSeconds { get; set; }

    public event Action Dismissed;

    public bool IsOpen => Visible;

    public override void _Ready()
    {
        title = GetNode<Label>("Sheet/Column/Title");
        lead = GetNode<Label>("Sheet/Column/Lead");
        body = GetNode<Label>("Sheet/Column/Body");
        small = GetNode<Label>("Sheet/Column/Small");
        no = GetNode<Button>("Sheet/Column/Answers/No");
        yes = GetNode<Button>("Sheet/Column/Answers/Yes");
        no.Pressed += () => Answer(false);
        yes.Pressed += () => Answer(true);
        GetNode<Button>("Sheet/Column/Policy").Pressed += () => OS.ShellOpen(ConsentWordings.PolicyUrl);
        GetNode<Control>("Wash").GuiInput += OnWashInput;
    }

    // changed: the player said yes to other wording, so this says what's paused and why
    public void Ask(ConsentWording wording, bool changed)
    {
        asking = wording;
        title.Text = changed ? wording.ChangedTitle : wording.AskTitle;
        lead.Text = wording.ChangedLead;
        lead.Visible = changed;
        body.Text = wording.Body;
        small.Text = wording.Small;
        no.Text = changed ? wording.ChangedNo : wording.No;
        yes.Text = changed ? wording.ChangedYes : wording.Yes;
        askedAt = Time.GetTicksMsec();
        Show();
    }

    public void Close()
    {
        if (!Visible)
        {
            return;
        }
        Hide();
        Dismissed?.Invoke();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Visible && @event.IsActionPressed("ui_cancel"))
        {
            GetViewport().SetInputAsHandled();
            Close();
        }
    }

    // Hidden before the answer is reported, so the next kind's ask can open the sheet again from the handler
    private void Answer(bool answer)
    {
        if (Time.GetTicksMsec() - askedAt < AnswerGuardSeconds * 1000)
        {
            return;
        }
        Hide();
        Answered?.Invoke(asking, answer);
    }

    // On release, as the settings sheet does, so the rest of the touch can't press what's under it
    private void OnWashInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false })
        {
            Close();
        }
    }
}
