using System;
using Godot;
using MergedCells.Core;

namespace MergedCells;

// The card over the faded board when a level is won. Menu stays disabled until chunk 11.
public partial class ResultsScreen : Control
{
    private Label titleLabel;
    private StarsView stars;
    private Label movesLabel;
    private Label parLabel;
    private Label bestLabel;
    private Button nextButton;

    public event Action RetryPressed;

    public event Action NextPressed;

    public string Title
    {
        set => titleLabel.Text = value;
    }

    public bool HasNext
    {
        set => nextButton.Disabled = !value;
    }

    public override void _Ready()
    {
        // The same insets as the HUD's title, so the two coincide over the wash
        titleLabel = GetNode<Label>("TitleBar/Row/TitleLabel");
        stars = GetNode<StarsView>("Sheet/Column/Stars");
        movesLabel = GetNode<Label>("Sheet/Column/MovesLabel");
        parLabel = GetNode<Label>("Sheet/Column/ParLabel");
        bestLabel = GetNode<Label>("Sheet/Column/BestLabel");
        GetNode<Button>("Sheet/Column/Buttons/Retry").Pressed += () => RetryPressed?.Invoke();
        nextButton = GetNode<Button>("Sheet/Column/Buttons/Next");
        nextButton.Pressed += () => NextPressed?.Invoke();
    }

    // A test board has no finish to compare with, so its stars show as earned or empty and it has no best line
    public void ShowResults(int moves, int par, int earned, int twoStarLimit, int oneStarLimit, LevelFinish finish)
    {
        stars.Looks = StarLooks.For(earned, finish is null ? earned : finish.PreviousStars ?? 0);
        movesLabel.Text = Plural(moves);
        bestLabel.Visible = finish?.PreviousBest is not null;
        if (finish?.PreviousBest is { } best)
        {
            bestLabel.Text = finish.IsNewBest ? $"New best · was {Plural(best)}" : $"Your best · {Plural(best)}";
        }
        parLabel.Text = earned switch
        {
            3 => $"Par {par}",
            2 => $"Par {par} · 3 stars at {par}",
            1 => $"Par {par} · 2 stars at {twoStarLimit}",
            _ => $"Par {par} · 1 star at {oneStarLimit}",
        };
        Show();
    }

    private static string Plural(int moves) => moves == 1 ? "1 move" : $"{moves} moves";
}
