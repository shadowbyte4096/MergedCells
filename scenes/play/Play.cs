using System;
using System.Globalization;
using Godot;
using MergedCells.Core;

namespace MergedCells;

// One level on screen: the board, the HUD, the slide animation, and the glue between input and the PlaySession
public partial class Play : Node2D, IScreen
{
    private BoardView board;
    private PlayerView player;
    private TrailView trail;
    private BoardCamera camera;
    private Control hudRoot;
    private Control topBar;
    private Control bottomBar;
    private BarBackdrop backdrop;
    private Control resultsTitleBar;
    private Label titleLabel;
    private Label movesLabel;
    private Button undoButton;
    private Button undoCaption;
    private PanelContainer undoBadge;
    private Label undoCount;
    private Control tutorialBlock;
    private Label tutorialLine;
    private Label tutorialDetail;
    private SwipeHint swipeHint;
    private ResultsScreen results;
    private SettingsSheet settings;

    private PlaySession session;
    private int par;
    private StarsConfig stars;
    private Func<bool> hasNext;
    private Func<int, LevelFinish> recordFinish;
    private Tutorial tutorial;

    private PlayerSlide? sliding;
    private double elapsed;
    private double duration;
    private bool dark;

    public event Action NextPressed;

    // The top bar's back chevron or the results card's Menu
    public event Action MenuPressed;

    public event Action<Appearance> AppearanceChosen;

    // The saved choice, set by the caller, so the settings sheet opens on it
    public Appearance Appearance { get; set; }

    [Export] public AppLooks Looks { get; set; }

    public bool Dark
    {
        get => dark;
        set
        {
            if (value != dark)
            {
                ApplyAppearance(value);
            }
        }
    }

    public override void _Ready()
    {
        board = GetNode<BoardView>("Board");
        player = board.GetNode<PlayerView>("Player");
        trail = board.GetNode<TrailView>("Trail");
        camera = GetNode<BoardCamera>("Camera");
        hudRoot = GetNode<Control>("Hud/Root");
        topBar = GetNode<Control>("Hud/Root/TopBar");
        bottomBar = GetNode<Control>("Hud/Root/BottomBar");
        backdrop = GetNode<BarBackdrop>("Hud/Root/BarBackdrop");
        resultsTitleBar = GetNode<Control>("Hud/Root/Results/TitleBar");
        titleLabel = GetNode<Label>("Hud/Root/TopBar/Row/Heading/TitleLabel");
        movesLabel = GetNode<Label>("Hud/Root/TopBar/Row/Heading/MovesLabel");
        results = GetNode<ResultsScreen>("Hud/Root/Results");
        settings = GetNode<SettingsSheet>("Hud/Root/Settings");
        undoButton = GetNode<Button>("Hud/Root/BottomBar/Row/Undo/UndoButton");
        undoCaption = GetNode<Button>("Hud/Root/BottomBar/Row/Undo/Caption");
        undoBadge = GetNode<PanelContainer>("Hud/Root/BottomBar/Row/Undo/UndoButton/Badge");
        undoCount = GetNode<Label>("Hud/Root/BottomBar/Row/Undo/UndoButton/Badge/Count");
        tutorialBlock = GetNode<Control>("Hud/Root/Tutorial");
        tutorialLine = GetNode<Label>("Hud/Root/Tutorial/Lines/Line");
        tutorialDetail = GetNode<Label>("Hud/Root/Tutorial/Lines/Detail");
        swipeHint = GetNode<SwipeHint>("Hud/Root/SwipeHint");

        GetNode<MoveInput>("Input").MoveRequested += OnMoveRequested;
        // Each caption is a flat button too, so tapping the word works like tapping the icon
        undoButton.Pressed += Undo;
        undoCaption.Pressed += Undo;
        undoButton.GuiInput += OnGreyedUndoInput;
        undoCaption.GuiInput += OnGreyedUndoInput;
        GetNode<Button>("Hud/Root/BottomBar/Row/Restart/RestartButton").Pressed += Restart;
        GetNode<Button>("Hud/Root/BottomBar/Row/Restart/Caption").Pressed += Restart;
        results.RetryPressed += Retry;
        results.NextPressed += () => NextPressed?.Invoke();
        results.MenuPressed += () => MenuPressed?.Invoke();
        GetNode<Button>("Hud/Root/TopBar/Row/Back").Pressed += () => MenuPressed?.Invoke();
        GetNode<Button>("Hud/Root/TopBar/Row/SettingsButton").Pressed += OpenSettings;
        settings.AppearanceChosen += choice =>
        {
            Appearance = choice;
            AppearanceChosen?.Invoke(choice);
        };
        // Never under the results card: a winning move remembered before the sheet opened can finish the level
        settings.Closed += () =>
        {
            if (Playing)
            {
                camera.InputEnabled = true;
            }
        };
        topBar.Resized += UpdateInsets;
        bottomBar.Resized += UpdateInsets;
        tutorialBlock.Resized += UpdateInsets;
        GetViewport().SizeChanged += ApplySafeArea;
        board.ThemeChanged += SetClearColor;
        ApplyAppearance(false);
        ApplySafeArea();
    }

    private void ApplyAppearance(bool toDark)
    {
        dark = toDark;
        var look = Looks.Look(dark);
        board.Theme = Looks.Board(dark);
        board.GetNode<StartMarker>("StartMarker").Look = look;
        trail.Look = look;
        player.Look = look;
        GetNode<Control>("Hud/Root").Theme = Looks.Ui(dark);
    }

    public bool CloseOverlay()
    {
        var open = settings.IsOpen;
        settings.Close();
        return open;
    }

    // stars are the level's thresholds (its chapter's in the campaign). hasNext is asked when the results card appears,
    // so a finish that opens the next chapter enables Next. recordFinish is called with the move count when the level is
    // won, and returns how the finish compares with the saved best; Play never sees level IDs or the save. Null (test
    // boards) records nothing. Null rules mean unlimited undo; a null tutorial shows no text.
    public void Start(
        Map map,
        int par,
        string title,
        StarsConfig stars,
        Func<bool> hasNext,
        Func<int, LevelFinish> recordFinish = null,
        PlayRules rules = null,
        Tutorial tutorial = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentOutOfRangeException.ThrowIfLessThan(par, 1);
        ArgumentException.ThrowIfNullOrEmpty(title);
        ArgumentNullException.ThrowIfNull(stars);
        ArgumentNullException.ThrowIfNull(hasNext);

        this.par = par;
        this.stars = stars;
        this.hasNext = hasNext;
        this.recordFinish = recordFinish;
        this.tutorial = tutorial;
        session = new PlaySession(map, rules);
        board.Map = map;
        board.GetNode<StartMarker>("StartMarker").Cell = map.Start;
        StopSlide();
        results.Hide();
        settings.Close();
        player.Cell = session.Position;
        titleLabel.Text = title;
        results.Title = title;
        RefreshMoves();
        RefreshUndo();
        ShowTutorial();
        SetClearColor();
        UpdateInsets();
        camera.InputEnabled = true;
        // Open applies the view to the canvas at once, which the board would otherwise read a frame late
        camera.Open(board.Bounds, board.CellCentre(map.Start));
        board.UpdateView();
    }

    public override void _Process(double delta)
    {
        UpdateSwipeHint();
        if (sliding is null)
        {
            return;
        }

        elapsed += delta;
        // A loop, because a long frame can finish one slide and part of the remembered one after it
        while (sliding is { } current && elapsed >= duration)
        {
            player.Cell = current.To;
            trail.Stretch(board.CellCentre(current.To));
            trail.Release();
            var leftover = elapsed - duration;
            sliding = null;
            if (session.FinishSlide() is { } next)
            {
                Begin(next);
                elapsed = leftover;
            }
            else if (session.IsFinished)
            {
                Finish();
            }
        }

        if (sliding is { } moving)
        {
            var progress = (float)(elapsed / duration);
            player.Position = board.CellCentre(moving.From).Lerp(board.CellCentre(moving.To), progress);
            trail.Stretch(player.Position);
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Echo: false } key)
        {
            return;
        }
        if (key.IsActionPressed("undo"))
        {
            GetViewport().SetInputAsHandled();
            Undo();
        }
        else if (key.IsActionPressed("restart"))
        {
            GetViewport().SetInputAsHandled();
            Restart();
        }
    }

    private bool Playing => session is not null && !session.IsFinished;

    private void OnMoveRequested(Direction direction)
    {
        if (Playing && !settings.IsOpen && session.Request(direction) is { } slide)
        {
            Begin(slide);
        }
    }

    private void OpenSettings()
    {
        // Once the winning move has started, the results are on their way
        if (session is null || session.IsWon || settings.IsOpen)
        {
            return;
        }
        settings.Open(Appearance);
        camera.InputEnabled = false;
    }

    private void Undo()
    {
        if (!Playing || settings.IsOpen)
        {
            return;
        }
        if (session.Undo() is not { } undone)
        {
            if (session.UndosLeft == 0 && !session.IsWon)
            {
                GD.Print("Undo: none left");
            }
            return;
        }
        StopSlide();
        player.Cell = session.Position;
        camera.Recentre(board.CellCentre(session.Position));
        RefreshMoves();
        RefreshUndo();
        var left = session.UndosLeft is { } count ? $" ({count} left)" : "";
        GD.Print($"Undo {undone}, back to {Format(session.Position)}{left}");
    }

    // A disabled button sends no Pressed, so a tap on the greyed Undo is caught here, for its "none left" log line
    private void OnGreyedUndoInput(InputEvent @event)
    {
        if (undoButton.Disabled && @event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false })
        {
            Undo();
        }
    }

    private void Restart()
    {
        if (Playing && !settings.IsOpen)
        {
            RestartLevel();
        }
    }

    private void Retry()
    {
        results.Hide();
        tutorialBlock.Modulate = Colors.White;
        camera.InputEnabled = true;
        RestartLevel();
    }

    private void RestartLevel()
    {
        session.Restart();
        StopSlide();
        player.Cell = session.Position;
        camera.Recentre(board.CellCentre(session.Position));
        RefreshMoves();
        RefreshUndo();
        GD.Print("Restart");
    }

    private void Begin(PlayerSlide slide)
    {
        // Worked out first, so a throw leaves no half-started slide behind
        duration = SlideTiming.Seconds(slide.Squares, GameConfigFile.Current.Slide);
        sliding = slide;
        elapsed = 0;
        trail.Begin(board.CellCentre(slide.From));
        camera.Follow(board.CellCentre(slide.From), board.CellCentre(slide.To), duration);
        RefreshMoves();
        var won = slide.Wins ? ", won" : "";
        GD.Print($"Move {DirectionOf(slide)} to {Format(slide.To)}{won}");
    }

    private void StopSlide()
    {
        sliding = null;
        elapsed = 0;
        trail.Clear();
    }

    private void Finish()
    {
        var earned = Stars.Earned(session.Moves, par, stars);
        var finish = recordFinish?.Invoke(session.Moves);
        // Transparent rather than hidden, so the board's inset, and the board, stay where they are
        tutorialBlock.Modulate = Colors.Transparent;
        settings.Close();
        camera.InputEnabled = false;
        results.HasNext = hasNext();
        results.ShowResults(session.Moves, par, earned, Stars.TwoStarLimit(par, stars), Stars.OneStarLimit(par, stars), finish);
        GD.Print($"Results: {Plural(session.Moves, "move")}, par {par}, {Plural(earned, "star")}");
    }

    private void RefreshMoves() => movesLabel.Text = $"{Plural(session.Moves, "move")} · par {par}";

    // No badge when undo is unlimited. With none left, Undo greys out until a restart.
    private void RefreshUndo()
    {
        var left = session.UndosLeft;
        undoBadge.Visible = left is not null;
        var empty = left == 0;
        undoButton.Disabled = empty;
        undoCaption.Disabled = empty;
        if (left is { } count)
        {
            undoCount.Text = count.ToString(CultureInfo.InvariantCulture);
            undoBadge.ThemeTypeVariation = empty ? "UndoBadgeEmpty" : "UndoBadge";
            undoCount.ThemeTypeVariation = empty ? "UndoBadgeTextEmpty" : "UndoBadgeText";
        }
    }

    // A tutorial's text sits below the top bar, so the board fits below the text
    private void UpdateInsets()
    {
        var top = tutorialBlock.Visible ? tutorialBlock.GetRect().End.Y : topBar.GetRect().End.Y;
        camera.SetInsets(top, hudRoot.Size.Y - bottomBar.Position.Y);
        backdrop.SetEdges(topBar.GetRect().End.Y, bottomBar.Position.Y);
    }

    // Phones get the swipe wording and the ghost finger; desktop gets the keyboard wording
    private void ShowTutorial()
    {
        tutorialBlock.Visible = tutorial is not null;
        tutorialBlock.Modulate = Colors.White;
        if (tutorial is null)
        {
            swipeHint.Direction = null;
            return;
        }
        var mobile = OS.HasFeature("mobile");
        var lines = mobile ? tutorial.Lines : tutorial.KeyboardLines;
        tutorialLine.Text = lines[0];
        tutorialDetail.Text = lines.Count > 1 ? lines[1] : "";
        tutorialDetail.Visible = lines.Count > 1;
        swipeHint.Direction = mobile ? tutorial.SwipeHint : null;
    }

    // Shown only before the first move; placed each frame, as zoom and pan move the board
    private void UpdateSwipeHint()
    {
        if (swipeHint.Direction is null)
        {
            return;
        }
        if (!Playing || session.Moves > 0)
        {
            swipeHint.Visible = false;
            return;
        }
        var boardBottom = (board.GetGlobalTransformWithCanvas() * new Vector2(0, board.Bounds.End.Y)).Y;
        swipeHint.Place(boardBottom, bottomBar.Position.Y);
    }

    // The game draws under Android's camera hole, so the bars move into the safe area. The results sheet stays
    // flush with the bottom edge, as its own padding already clears a gesture bar.
    private void ApplySafeArea()
    {
        var (top, bottom) = SafeArea.Insets(this);
        foreach (var bar in new[] { topBar, resultsTitleBar })
        {
            bar.OffsetTop = top;
            bar.OffsetBottom = top + bar.GetCombinedMinimumSize().Y;
        }
        bottomBar.OffsetBottom = -bottom;
        bottomBar.OffsetTop = -bottom - bottomBar.GetCombinedMinimumSize().Y;
        // Grows down to fit its text from the top bar's bottom edge
        tutorialBlock.OffsetTop = topBar.OffsetBottom;
        tutorialBlock.OffsetBottom = topBar.OffsetBottom;
        UpdateInsets();
    }

    private void SetClearColor()
    {
        RenderingServer.SetDefaultClearColor(board.Theme.Background);
        backdrop.Color = board.Theme.Background;
    }

    private static string Plural(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";

    private static string Format(Cell cell) => $"[{cell.Row}, {cell.Column}]";

    private static Direction DirectionOf(PlayerSlide slide) =>
        slide.To.Row != slide.From.Row
            ? (slide.To.Row < slide.From.Row ? Direction.Up : Direction.Down)
            : (slide.To.Column < slide.From.Column ? Direction.Left : Direction.Right);
}
