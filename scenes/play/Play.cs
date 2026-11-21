using System;
using System.Globalization;
using Godot;
using MergedCells.Core;

namespace MergedCells;

// One level on screen: the board, the HUD, the slide animation, and the glue between input and the PlaySession. It also
// waits while the next infinite map is made (Next map's bar on the card, or New map's ring)
public partial class Play : Node2D, IScreen, IMapWait
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
    private Button restartButton;
    private Button restartCaption;
    private Control newMap;
    private Button newMapButton;
    private Button newMapCaption;
    private bool newMapArmed;
    private MakingRing makingRing;
    // From the press that asks for the next map until it arrives (or Cancel): this map takes no input
    private bool waiting;
    private bool waitShowing;
    private bool waitFromResults;
    private Control tutorialBlock;
    private Label tutorialLine;
    private Label tutorialDetail;
    private SwipeHint swipeHint;
    private ResultsScreen results;
    private SettingsSheet settings;

    private PlaySession session;
    private int par;
    private PlaySetup setup;
    private PlayRecorder recorder;
    // Time with the app in front, for play records
    private readonly ActiveClock clock = new();

    private PlayerSlide? sliding;
    private MoveTimeline timeline;
    private double elapsed;
    // How many of the slide in flight's events have run
    private int eventsRun;
    private bool dark;

    public event Action NextPressed;

    // The confirming second tap on New map, after this map's play record has ended as given up
    public event Action NewMapPressed;

    // The top bar's back chevron or the results card's Menu
    public event Action MenuPressed;

    public event Action WaitCancelled;

    [Export] public AppLooks Looks { get; set; }

    public SettingsSheet Settings => settings;

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
        newMap = GetNode<Control>("Hud/Root/BottomBar/Row/NewMap");
        newMapButton = GetNode<Button>("Hud/Root/BottomBar/Row/NewMap/NewMapButton");
        newMapCaption = GetNode<Button>("Hud/Root/BottomBar/Row/NewMap/Caption");
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
        restartButton = GetNode<Button>("Hud/Root/BottomBar/Row/Restart/RestartButton");
        restartCaption = GetNode<Button>("Hud/Root/BottomBar/Row/Restart/Caption");
        restartButton.Pressed += Restart;
        restartCaption.Pressed += Restart;
        newMapButton.Pressed += OnNewMapPressed;
        newMapCaption.Pressed += OnNewMapPressed;
        makingRing = new MakingRing { Visible = false };
        newMapButton.AddChild(makingRing);
        makingRing.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        results.RetryPressed += Retry;
        results.NextPressed += () =>
        {
            if (!waiting)
            {
                NextPressed?.Invoke();
            }
        };
        results.MenuPressed += () =>
        {
            if (!waiting)
            {
                MenuPressed?.Invoke();
            }
        };
        results.CancelPressed += () => WaitCancelled?.Invoke();
        GetNode<Button>("Hud/Root/TopBar/Row/Back").Pressed += () => MenuPressed?.Invoke();
        GetNode<Button>("Hud/Root/TopBar/Row/SettingsButton").Pressed += OpenSettings;
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

    public bool CloseOverlay() => settings.Back();

    // Play never sees level IDs or the save: the setup's delegates record finishes and play data
    public void Start(Map map, int par, PlaySetup setup)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentOutOfRangeException.ThrowIfLessThan(par, 1);
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentException.ThrowIfNullOrEmpty(setup.Title);

        // The map being replaced was left unfinished
        EndRecording(PlayOutcome.Left);
        EndWait();
        this.par = par;
        this.setup = setup;
        var title = setup.Title;
        session = new PlaySession(map, setup.Rules);
        StartRecording();
        board.Map = map;
        board.GetNode<StartMarker>("StartMarker").Cell = map.Start;
        StopSlide();
        results.Hide();
        settings.Close();
        newMap.Visible = setup.NewMap;
        DisarmNewMap();
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

    public override void _Notification(int what)
    {
        if (what == NotificationApplicationFocusOut || what == NotificationApplicationPaused)
        {
            clock.Pause();
        }
        else if (what == NotificationApplicationFocusIn || what == NotificationApplicationResumed)
        {
            clock.Resume();
        }
    }

    // Leaving the screen (back, Menu, the app closing on desktop) leaves an unfinished map
    public override void _ExitTree() => EndRecording(PlayOutcome.Left);

    private long ActiveMs => clock.Milliseconds;

    private void StartRecording()
    {
        clock.Reset();
        recorder = setup.Recording?.Invoke();
    }

    private void EndRecording(PlayOutcome outcome)
    {
        recorder?.End(outcome, session.Moves, ActiveMs);
        recorder = null;
    }

    public override void _Process(double delta)
    {
        clock.Tick(delta);
        UpdateSwipeHint();
        if (sliding is null)
        {
            return;
        }

        elapsed += delta;
        // A loop, because a long frame can pass several of a move's events, finish it, and start the remembered one
        while (sliding is { } current)
        {
            // One event per pass, in the timeline's order, so a frame that skips past several still runs them all. A jump's
            // wait moves every later time, so the next event's time is read only after it is set
            if (eventsRun < timeline.Events.Count && elapsed >= timeline.Events[eventsRun].At)
            {
                RunEvent(current, timeline.Events[eventsRun++]);
                continue;
            }
            if (elapsed < timeline.Seconds)
            {
                break;
            }
            if (!current.Trapped)
            {
                trail.Stretch(board.CellCentre(current.To));
                trail.Release();
            }
            player.Cell = current.To;
            SetPlayerScale(1);
            var leftover = elapsed - timeline.Seconds;
            sliding = null;
            // The map was given up for a new one: a remembered move mustn't start, or finish it
            if (waiting)
            {
                break;
            }
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

        if (sliding is null)
        {
            return;
        }
        var frame = timeline.At(elapsed);
        player.Position = board.CellCentre(frame.From).Lerp(board.CellCentre(frame.To), (float)frame.Progress);
        SetPlayerScale(frame.Scale);
        if (frame.Sliding)
        {
            trail.Stretch(player.Position);
        }
    }

    private void RunEvent(PlayerSlide slide, MoveEvent moveEvent)
    {
        switch (moveEvent.Kind)
        {
            case MoveEventKind.ReachTeleporter:
                ReachTeleporter(slide, moveEvent.Index);
                break;
            case MoveEventKind.BeginRun:
                BeginRun(slide, moveEvent.Index);
                break;
            case MoveEventKind.Catch:
                Catch(slide);
                break;
        }
    }

    // The player has slid into a teleporter: its trail ends there, and the camera may need time to show the partner
    private void ReachTeleporter(PlayerSlide slide, int jump)
    {
        trail.Stretch(board.CellCentre(slide.Runs[jump].To));
        trail.Release();
        timeline = timeline.WithWait(jump, camera.Reveal(board.CellCentre(slide.Runs[jump + 1].From)));
    }

    // The player has grown out of a partner and slides on: a new trail strip, followed like any slide
    private void BeginRun(PlayerSlide slide, int run)
    {
        var from = board.CellCentre(slide.Runs[run].From);
        trail.Begin(from);
        camera.Follow(from, board.CellCentre(slide.Runs[run].To), timeline.RunEnd(run) - timeline.RunStart(run));
    }

    // The slide has reached a trap: the player shrinks into it and grows back on the start, while the camera heads there
    private void Catch(PlayerSlide slide)
    {
        player.Cell = slide.Reached;
        trail.Stretch(board.CellCentre(slide.Reached));
        trail.Release();
        camera.Recentre(board.CellCentre(slide.To));
    }

    // Hidden at 0: a node scaled to exactly 0 has a singular transform
    private void SetPlayerScale(double scale)
    {
        player.Visible = scale > 0;
        if (scale > 0)
        {
            player.Scale = new Vector2((float)scale, (float)scale);
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
        else if (key.IsActionPressed("new_map") && newMap.Visible)
        {
            GetViewport().SetInputAsHandled();
            OnNewMapPressed();
        }
    }

    // The first press arms it and the second gives up on the map; anything else in between disarms it, so there's no timer
    private void OnNewMapPressed()
    {
        if (!Playing || session.IsWon || settings.IsOpen || waiting)
        {
            return;
        }
        if (!newMapArmed)
        {
            newMapArmed = true;
            newMapButton.ThemeTypeVariation = "RoundButtonArmed";
            newMapCaption.Text = "Tap again";
            GD.Print("New map: armed");
            return;
        }
        DisarmNewMap();
        GD.Print("New map");
        EndRecording(PlayOutcome.GaveUp);
        NewMapPressed?.Invoke();
    }

    private void DisarmNewMap()
    {
        newMapArmed = false;
        newMapButton.ThemeTypeVariation = "";
        newMapCaption.Text = "New map";
    }

    private bool Playing => session is not null && !session.IsFinished;

    private void OnMoveRequested(Direction direction)
    {
        if (Playing && !settings.IsOpen && !waiting && session.Request(direction) is { } slide)
        {
            Begin(slide);
        }
    }

    // A finished map would close an open sheet, or reach a freed one from Export's save picker, so the gear waits too
    private void OpenSettings()
    {
        // Once the winning move has started, the results are on their way
        if (session is null || session.IsWon || settings.IsOpen || waiting)
        {
            return;
        }
        DisarmNewMap();
        settings.Open();
        camera.InputEnabled = false;
    }

    private void Undo()
    {
        if (!Playing || settings.IsOpen || waiting)
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
        DisarmNewMap();
        recorder?.Undo(ActiveMs);
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
        if (Playing && !settings.IsOpen && !waiting)
        {
            RestartLevel();
            recorder?.Restart(ActiveMs);
        }
    }

    public void BeginWait()
    {
        waiting = true;
        waitFromResults = results.Visible;
    }

    public void ShowWait(double progress)
    {
        if (waitFromResults)
        {
            results.ShowMaking(progress);
            return;
        }
        if (!waitShowing)
        {
            waitShowing = true;
            newMapButton.ThemeTypeVariation = "RoundButtonMaking";
            newMapCaption.Text = "Making…";
            makingRing.Visible = true;
            foreach (var button in new[] { undoButton, undoCaption, restartButton, restartCaption })
            {
                button.Disabled = true;
            }
        }
        makingRing.Progress = progress;
    }

    // Also the start of every map, since a new map ends any wait
    public void EndWait()
    {
        waiting = false;
        waitShowing = false;
        results.EndMaking();
        makingRing.Visible = false;
        makingRing.Progress = 0;
        restartButton.Disabled = false;
        restartCaption.Disabled = false;
        DisarmNewMap();
        if (session is not null)
        {
            RefreshUndo();
        }
    }

    // A new visit to the same map, so a new play record
    private void Retry()
    {
        if (waiting)
        {
            return;
        }
        results.Hide();
        tutorialBlock.Modulate = Colors.White;
        camera.InputEnabled = true;
        RestartLevel();
        StartRecording();
    }

    private void RestartLevel()
    {
        session.Restart();
        StopSlide();
        DisarmNewMap();
        player.Cell = session.Position;
        camera.Recentre(board.CellCentre(session.Position));
        RefreshMoves();
        RefreshUndo();
        GD.Print("Restart");
    }

    private void Begin(PlayerSlide slide)
    {
        // Worked out first, so a throw leaves no half-started slide behind
        timeline = new MoveTimeline(slide, GameConfigFile.Current.Slide);
        sliding = slide;
        elapsed = 0;
        eventsRun = 0;
        DisarmNewMap();
        recorder?.Move(slide.Direction, ActiveMs);
        trail.Begin(board.CellCentre(slide.From));
        camera.Follow(board.CellCentre(slide.From), board.CellCentre(slide.Runs[0].To), timeline.RunEnd(0));
        RefreshMoves();
        var through = "";
        for (var jump = 0; jump < timeline.Jumps; jump++)
        {
            through += $", through {Format(slide.Runs[jump].To)} to {Format(slide.Runs[jump + 1].From)}";
        }
        var won = slide.Wins ? ", won" : "";
        var trapped = slide.Trapped ? $", trapped, back to {Format(slide.To)}" : "";
        GD.Print($"Move {slide.Direction} to {Format(slide.Reached)}{through}{won}{trapped}");
    }

    private void StopSlide()
    {
        sliding = null;
        elapsed = 0;
        SetPlayerScale(1);
        trail.Clear();
    }

    private void Finish()
    {
        // The best first: play data is for tuning, and must never cost a player their result
        var finish = setup.RecordFinish?.Invoke(session.Moves);
        EndRecording(PlayOutcome.Won);
        // Transparent rather than hidden, so the board's inset, and the board, stay where they are
        tutorialBlock.Modulate = Colors.Transparent;
        settings.Close();
        camera.InputEnabled = false;
        results.ShowNext = setup.HasNext is not null;
        results.NextText = setup.NextText;
        results.ShowLeaderboard(setup.Leaderboard?.Invoke());
        results.ShowLibrary(setup.Library?.Invoke());
        if (setup.HasNext is not null)
        {
            results.HasNext = setup.HasNext();
        }
        var stars = setup.Stars;
        if (stars is null)
        {
            results.ShowPlain(session.Moves, par);
            GD.Print($"Results: {Plural(session.Moves, "move")}, par {par}");
            return;
        }
        var earned = Stars.Earned(session.Moves, par, stars);
        results.ShowResults(session.Moves, par, earned, Stars.TwoStarLimit(par, stars), Stars.OneStarLimit(par, stars), finish);
        GD.Print($"Results: {Plural(session.Moves, "move")}, par {par}, {Plural(earned, "star")}");
    }

    private void RefreshMoves()
    {
        var moves = $"{Plural(session.Moves, "move")} · par {par}";
        movesLabel.Text = setup.Detail is null ? moves : $"{setup.Detail} · {moves}";
    }

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
        var tutorial = setup.Tutorial;
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
}
