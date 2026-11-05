using System;
using Godot;

namespace MergedCells;

// The card over the faded board when a level is won. Menu and Next stay disabled until chunks 11 and 7.
public partial class ResultsScreen : Control
{
    private Label titleLabel;
    private StarsView stars;
    private Label movesLabel;
    private Label parLabel;

    public event Action RetryPressed;

    public string Title
    {
        set => titleLabel.Text = value;
    }

    public override void _Ready()
    {
        // The same insets as the HUD's title, so the two coincide over the wash
        titleLabel = GetNode<Label>("TitleBar/Row/TitleLabel");
        stars = GetNode<StarsView>("Sheet/Column/Stars");
        movesLabel = GetNode<Label>("Sheet/Column/MovesLabel");
        parLabel = GetNode<Label>("Sheet/Column/ParLabel");
        GetNode<Button>("Sheet/Column/Buttons/Retry").Pressed += () => RetryPressed?.Invoke();
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
