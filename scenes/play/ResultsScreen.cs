using System;
using Godot;

namespace MergedCells;

// The popup over the dimmed board when a level is won. Menu and Next stay disabled until chunks 11 and 7.
public partial class ResultsScreen : Control
{
    private StarsView stars;
    private Label movesLabel;
    private Label parLabel;

    public event Action RetryPressed;

    public override void _Ready()
    {
        stars = GetNode<StarsView>("Centre/Panel/Column/Stars");
        movesLabel = GetNode<Label>("Centre/Panel/Column/MovesLabel");
        parLabel = GetNode<Label>("Centre/Panel/Column/ParLabel");
        GetNode<Button>("Centre/Panel/Column/Buttons/Retry").Pressed += () => RetryPressed?.Invoke();
    }

    public void ShowResults(int moves, int par, int earned, int twoStarLimit, int oneStarLimit)
    {
        stars.Earned = earned;
        movesLabel.Text = moves == 1 ? "1 move" : $"{moves} moves";
        parLabel.Text = earned switch
        {
            3 => $"Par {par}",
            2 => $"Par {par} · 3 stars at {par}",
            1 => $"Par {par} · 2 stars at {twoStarLimit}",
            _ => $"Par {par} · 1 star at {oneStarLimit}",
        };
        Show();
    }
}
