using System;
using Godot;
using MergedCells.Core;

namespace MergedCells;

// The card over the faded board when a level is won
public partial class ResultsScreen : Control
{
    private Label titleLabel;
    private StarsView stars;
    private Label movesLabel;
    private Label parLabel;
    private Label bestLabel;
    private Control runPanel;
    private TileLabels streakTile;
    private TileLabels runTile;
    private TileLabels efficiencyTile;
    private Control leaderboard;
    private BestMovesChart chart;
    private Control libraryPanel;
    private LibraryBox libraryBox;
    private Button retryButton;
    private Button menuButton;
    private Button nextButton;
    private MakingBar makingBar;
    private Button cancelButton;
    private bool making;

    public event Action RetryPressed;

    public event Action NextPressed;

    public event Action MenuPressed;

    // Cancel under Next map's bar while the next map is made
    public event Action CancelPressed;

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
        runPanel = GetNode<Control>("Sheet/Column/Run");
        streakTile = TileLabels.At(runPanel, "Tiles/Streak");
        runTile = TileLabels.At(runPanel, "Tiles/Run");
        efficiencyTile = TileLabels.At(runPanel, "Tiles/Efficiency");
        leaderboard = GetNode<Control>("Sheet/Column/Leaderboard");
        chart = new BestMovesChart();
        leaderboard.AddChild(chart);
        libraryPanel = GetNode<Control>("Sheet/Column/Library");
        libraryBox = new LibraryBox();
        libraryPanel.AddChild(libraryBox);
        retryButton = GetNode<Button>("Sheet/Column/Buttons/Retry");
        retryButton.Pressed += () => RetryPressed?.Invoke();
        menuButton = GetNode<Button>("Sheet/Column/Buttons/Menu");
        menuButton.Pressed += () => MenuPressed?.Invoke();
        nextButton = GetNode<Button>("Sheet/Column/Buttons/Next");
        nextButton.Pressed += () => NextPressed?.Invoke();
        BuildMaking();
    }

    // The bar fills Next's place and Cancel hangs below it inside the card's bottom padding, both outside the row's layout,
    // so the card doesn't move or grow
    private void BuildMaking()
    {
        makingBar = new MakingBar { Visible = false, Text = "Making…" };
        nextButton.AddChild(makingBar);
        makingBar.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        cancelButton = new Button { Visible = false, Text = "Cancel", ThemeTypeVariation = "CaptionButton", FocusMode = FocusModeEnum.None };
        nextButton.AddChild(cancelButton);
        cancelButton.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
        cancelButton.OffsetTop = 0;
        cancelButton.OffsetBottom = 0;
        cancelButton.GrowVertical = GrowDirection.End;
        cancelButton.Pressed += () => CancelPressed?.Invoke();
    }

    // Every frame once the next map has taken longer than the delay: Next map becomes its bar, Retry and Menu grey out
    public void ShowMaking(double progress)
    {
        if (!making)
        {
            making = true;
            nextButton.SelfModulate = Colors.Transparent;
            makingBar.Visible = true;
            cancelButton.Visible = true;
            retryButton.Disabled = true;
            menuButton.Disabled = true;
        }
        makingBar.Progress = progress;
    }

    public void EndMaking()
    {
        making = false;
        nextButton.SelfModulate = Colors.White;
        makingBar.Visible = false;
        makingBar.Progress = 0;
        cancelButton.Visible = false;
        retryButton.Disabled = false;
        menuButton.Disabled = false;
    }

    // Hides Next, for a level with no next one (a custom level), rather than greying it
    public bool ShowNext
    {
        set => nextButton.Visible = value;
    }

    public string NextText
    {
        set => nextButton.Text = value;
    }

    // Retry and a new map hide the card: its chart lets go of the fetch, so one still waiting is never sent
    public override void _Notification(int what)
    {
        if (what == NotificationVisibilityChanged && !Visible && chart is not null)
        {
            chart.Watch(null);
            libraryBox.Watch(null);
        }
    }

    // Campaign levels only, while leaderboards are shared and the server is on for the run; null hides the area
    public void ShowLeaderboard(LeaderboardSource source)
    {
        leaderboard.Visible = source is not null;
        chart.Watch(source);
    }

    // A library level, while the library is given and the server is on for the run; null hides the box
    public void ShowLibrary(LibraryCardSource source)
    {
        libraryPanel.Visible = source is not null;
        libraryBox.Watch(source);
    }

    // A test board has no finish to compare with, so its stars show as earned or empty and it has no best line
    public void ShowResults(int moves, int par, int earned, int twoStarLimit, int oneStarLimit, LevelFinish finish)
    {
        stars.Visible = true;
        runPanel.Visible = false;
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

    // A level without stars (a custom level): the moves and par only
    public void ShowPlain(int moves, int par)
    {
        stars.Visible = false;
        runPanel.Visible = false;
        movesLabel.Text = Plural(moves);
        bestLabel.Visible = false;
        parLabel.Text = $"Par {par}";
        Show();
    }

    // An infinite map: the moves, its efficiency on the par line, its best line on a Retry, and the run's three tiles
    public void ShowRun(int moves, InfiniteCard card)
    {
        stars.Visible = false;
        movesLabel.Text = Plural(moves);
        parLabel.Text = card.ParLine;
        bestLabel.Visible = card.BestLine is not null;
        bestLabel.Text = card.BestLine ?? "";
        streakTile.Fill(card.Streak);
        runTile.Fill(card.Run);
        efficiencyTile.Fill(card.Efficiency);
        runPanel.Visible = true;
        Show();
    }

    private static string Plural(int moves) => moves == 1 ? "1 move" : $"{moves} moves";
}

// One tile's three labels; the setup screen's tiles have no note
public sealed record TileLabels(Label Key, Label Value, Label Note)
{
    public static TileLabels At(Node parent, string path) =>
        new(parent.GetNode<Label>($"{path}/Key"), parent.GetNode<Label>($"{path}/Value"), parent.GetNodeOrNull<Label>($"{path}/Note"));

    public void Fill(StatTile tile)
    {
        Key.Text = tile.Key;
        Value.Text = tile.Value;
        if (Note is not null)
        {
            Note.Text = tile.Note;
            Note.ThemeTypeVariation = tile.Strong ? "StatNoteStrong" : "StatNote";
        }
    }
}
