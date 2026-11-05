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
    private Control topBar;
    private Control bottomBar;
    private Label titleLabel;
    private Label movesLabel;
    private ResultsScreen results;

    private PlaySession session;
    private int par;

    private PlayerSlide? sliding;
    private double elapsed;
    private double duration;
    private bool dark;

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
        topBar = GetNode<Control>("Hud/Root/TopBar");
        bottomBar = GetNode<Control>("Hud/Root/BottomBar");
        titleLabel = GetNode<Label>("Hud/Root/TopBar/Row/Heading/TitleLabel");
        movesLabel = GetNode<Label>("Hud/Root/TopBar/Row/Heading/MovesLabel");
        results = GetNode<ResultsScreen>("Hud/Root/Results");

        GetNode<MoveInput>("Input").MoveRequested += OnMoveRequested;
        // Each caption is a flat button too, so tapping the word works like tapping the icon
        GetNode<Button>("Hud/Root/BottomBar/Row/Undo/UndoButton").Pressed += Undo;
        GetNode<Button>("Hud/Root/BottomBar/Row/Undo/Caption").Pressed += Undo;
        GetNode<Button>("Hud/Root/BottomBar/Row/Restart/RestartButton").Pressed += Restart;
        GetNode<Button>("Hud/Root/BottomBar/Row/Restart/Caption").Pressed += Restart;
        results.RetryPressed += Retry;
        topBar.Resized += UpdateInsets;
        bottomBar.Resized += UpdateInsets;
        board.ThemeChanged += SetClearColor;
        ApplyAppearance(false);
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

    public void Start(Map map, int par, string title)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentOutOfRangeException.ThrowIfLessThan(par, 1);
        ArgumentException.ThrowIfNullOrEmpty(title);

        this.par = par;
        session = new PlaySession(map);
        board.Map = map;
        board.GetNode<StartMarker>("StartMarker").Cell = map.Start;
        StopSlide();
        results.Hide();
        player.Cell = session.Position;
        titleLabel.Text = title;
        results.Title = title;
        RefreshMoves();
        SetClearColor();
        UpdateInsets();
        camera.Fit(board.Bounds);
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
        if (Playing && session.Request(direction) is { } slide)
        {
            Begin(slide);
        }
    }

    private void Undo()
    {
        if (!Playing || session.Undo() is not { } undone)
        {
            return;
        }
        StopSlide();
        player.Cell = session.Position;
        RefreshMoves();
        GD.Print($"Undo {undone}, back to {Format(session.Position)}");
    }

    private void Restart()
    {
        if (Playing)
        {
            RestartLevel();
        }
    }

    private void Retry()
    {
        results.Hide();
        RestartLevel();
    }

    private void RestartLevel()
    {
        session.Restart();
        StopSlide();
        player.Cell = session.Position;
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
        results.ShowResults(session.Moves, par, stars, Stars.TwoStarLimit(par, config), Stars.OneStarLimit(par, config));
        GD.Print($"Results: {Plural(session.Moves, "move")}, par {par}, {Plural(stars, "star")}");
    }

    private void RefreshMoves() => movesLabel.Text = $"{Plural(session.Moves, "move")} · par {par}";

    private void UpdateInsets() => camera.SetInsets(topBar.Size.Y, bottomBar.Size.Y);

    private void SetClearColor() => RenderingServer.SetDefaultClearColor(board.Theme.Background);

    private static string Plural(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";

    private static string Format(Cell cell) => $"[{cell.Row}, {cell.Column}]";

    private static Direction DirectionOf(PlayerSlide slide) =>
        slide.To.Row != slide.From.Row
            ? (slide.To.Row < slide.From.Row ? Direction.Up : Direction.Down)
            : (slide.To.Column < slide.From.Column ? Direction.Left : Direction.Right);
}
