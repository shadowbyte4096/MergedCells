using System;
using Godot;
using MergedCells.Core;

namespace MergedCells;

// One level on screen: the board, the HUD, the slide animation, and the glue between input and the PlaySession
public partial class Play : Node2D
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
    private ResultsScreen results;
    private SettingsSheet settings;

    private PlaySession session;
    private int par;
    private Func<int, LevelFinish> recordFinish;

    private PlayerSlide? sliding;
    private double elapsed;
    private double duration;
    private bool dark;

    public event Action NextPressed;

    public event Action<Appearance> AppearanceChosen;

    // The saved choice, set by the caller, so the settings sheet opens on it
    public Appearance Appearance { get; set; }

    [Export] public BoardTheme LightBoard { get; set; }

    [Export] public BoardTheme DarkBoard { get; set; }

    [Export] public PlayerLook LightLook { get; set; }

    [Export] public PlayerLook DarkLook { get; set; }

    [Export] public Theme LightUi { get; set; }

    [Export] public Theme DarkUi { get; set; }

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

        GetNode<MoveInput>("Input").MoveRequested += OnMoveRequested;
        // Each caption is a flat button too, so tapping the word works like tapping the icon
        GetNode<Button>("Hud/Root/BottomBar/Row/Undo/UndoButton").Pressed += Undo;
        GetNode<Button>("Hud/Root/BottomBar/Row/Undo/Caption").Pressed += Undo;
        GetNode<Button>("Hud/Root/BottomBar/Row/Restart/RestartButton").Pressed += Restart;
        GetNode<Button>("Hud/Root/BottomBar/Row/Restart/Caption").Pressed += Restart;
        results.RetryPressed += Retry;
        results.NextPressed += () => NextPressed?.Invoke();
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
        GetViewport().SizeChanged += ApplySafeArea;
        board.ThemeChanged += SetClearColor;
        ApplyAppearance(false);
        ApplySafeArea();
    }

    private void ApplyAppearance(bool toDark)
    {
        dark = toDark;
        var look = dark ? DarkLook : LightLook;
        board.Theme = dark ? DarkBoard : LightBoard;
        board.GetNode<StartMarker>("StartMarker").Look = look;
        trail.Look = look;
        player.Look = look;
        GetNode<Control>("Hud/Root").Theme = dark ? DarkUi : LightUi;
    }

    // recordFinish is called with the move count when the level is won, and returns how the finish compares with the
    // saved best. Play never sees level IDs or the save. Null (test boards) records nothing.
    public void Start(Map map, int par, string title, bool hasNext, Func<int, LevelFinish> recordFinish = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentOutOfRangeException.ThrowIfLessThan(par, 1);
        ArgumentException.ThrowIfNullOrEmpty(title);

        this.par = par;
        this.recordFinish = recordFinish;
        session = new PlaySession(map);
        board.Map = map;
        board.GetNode<StartMarker>("StartMarker").Cell = map.Start;
        StopSlide();
        results.Hide();
        settings.Close();
        player.Cell = session.Position;
        titleLabel.Text = title;
        results.Title = title;
        results.HasNext = hasNext;
        RefreshMoves();
        SetClearColor();
        UpdateInsets();
        camera.InputEnabled = true;
        // Open applies the view to the canvas at once, which the board would otherwise read a frame late
        camera.Open(board.Bounds, board.CellCentre(map.Start));
        board.UpdateView();
    }

    public override void _Process(double delta)
    {
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
        if (!Playing || settings.IsOpen || session.Undo() is not { } undone)
        {
            return;
        }
        StopSlide();
        player.Cell = session.Position;
        camera.Recentre(board.CellCentre(session.Position));
        RefreshMoves();
        GD.Print($"Undo {undone}, back to {Format(session.Position)}");
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
        var config = GameConfigFile.Current.Stars;
        var stars = Stars.Earned(session.Moves, par, config);
        var finish = recordFinish?.Invoke(session.Moves);
        settings.Close();
        camera.InputEnabled = false;
        results.ShowResults(session.Moves, par, stars, Stars.TwoStarLimit(par, config), Stars.OneStarLimit(par, config), finish);
        GD.Print($"Results: {Plural(session.Moves, "move")}, par {par}, {Plural(stars, "star")}");
    }

    private void RefreshMoves() => movesLabel.Text = $"{Plural(session.Moves, "move")} · par {par}";

    private void UpdateInsets()
    {
        camera.SetInsets(topBar.GetRect().End.Y, hudRoot.Size.Y - bottomBar.Position.Y);
        backdrop.SetEdges(topBar.GetRect().End.Y, bottomBar.Position.Y);
    }

    // The game draws under Android's camera hole, so the bars move into the safe area. The results sheet stays
    // flush with the bottom edge, as its own padding already clears a gesture bar.
    private void ApplySafeArea()
    {
        var (top, bottom) = SafeInsets();
        foreach (var bar in new[] { topBar, resultsTitleBar })
        {
            bar.OffsetTop = top;
            bar.OffsetBottom = top + bar.GetCombinedMinimumSize().Y;
        }
        bottomBar.OffsetBottom = -bottom;
        bottomBar.OffsetTop = -bottom - bottomBar.GetCombinedMinimumSize().Y;
        UpdateInsets();
    }

    // In viewport units. Desktop windows aren't cut into, and there the safe area is only the screen's usable rect.
    private (float Top, float Bottom) SafeInsets()
    {
        if (!OS.HasFeature("mobile"))
        {
            return (0, 0);
        }
        var safe = DisplayServer.GetDisplaySafeArea();
        var window = new Rect2I(DisplayServer.WindowGetPosition(), DisplayServer.WindowGetSize());
        var scale = GetTree().Root.GetFinalTransform().Scale.Y;
        var top = Math.Max(0, safe.Position.Y - window.Position.Y) / scale;
        var bottom = Math.Max(0, window.End.Y - safe.End.Y) / scale;
        GD.Print($"Safe area: top {top:0.#}, bottom {bottom:0.#}");
        return (top, bottom);
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
