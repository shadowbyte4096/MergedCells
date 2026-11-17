using System;
using System.Diagnostics;
using Godot;
using MergedCells.Core;

namespace MergedCells;

// The level editor: the game's board under a camera, one tool at a time, and the status line. Every rule lives in Core
// (LevelEdits, EditStroke, EditSession, LevelCheck); this screen maps a finished stroke to an edit and shows the result.
// Main keeps the EditSession, so the undo history survives "Play it" and back.
public partial class LevelEditor : Node2D, IScreen
{
    private BoardView board;
    private PlayerView player;
    private StartMarker startMarker;
    private TrailView trail;
    private StrokeOverlay overlay;
    private BoardCamera camera;
    private EditorInput input;
    private Control hudRoot;
    private Control topBar;
    private Control bottomBar;
    private BarBackdrop backdrop;
    private Button nameButton;
    private Label status;
    private Button undoButton;
    private Button redoButton;
    private Button playButton;
    private RenameSheet renameSheet;
    private BoardSheet boardSheet;

    private EditSession session;
    private EditorTool tool;
    private DifficultyRater rater;
    private CustomLevelsConfig config;
    private LevelStatus levelStatus;
    private Cell? pendingTeleporter;
    private string saveProblem;
    private bool dark;

    public event Action BackPressed;

    public event Action<CustomLevel> PlayPressed;

    // The level after every change, for Main to write
    public event Action<CustomLevel> Changed;

    public event Action<EditorTool> ToolChosen;

    [Export] public AppLooks Looks { get; set; }

    // Why the level's last change wasn't saved, set by Main after each write, or null while saving works. Editing carries on
    // in memory, so the player should know before they leave
    public string SaveProblem
    {
        set
        {
            saveProblem = value;
            if (session is not null)
            {
                ShowStatus();
            }
        }
    }

    public bool Dark
    {
        set
        {
            dark = value;
            ApplyAppearance();
        }
    }

    public override void _Ready()
    {
        board = GetNode<BoardView>("Board");
        player = board.GetNode<PlayerView>("Player");
        startMarker = board.GetNode<StartMarker>("StartMarker");
        trail = board.GetNode<TrailView>("Trail");
        overlay = board.GetNode<StrokeOverlay>("StrokeOverlay");
        camera = GetNode<BoardCamera>("Camera");
        input = GetNode<EditorInput>("Input");
        hudRoot = GetNode<Control>("Hud/Root");
        topBar = GetNode<Control>("Hud/Root/TopBar");
        bottomBar = GetNode<Control>("Hud/Root/BottomBar");
        backdrop = GetNode<BarBackdrop>("Hud/Root/BarBackdrop");
        nameButton = GetNode<Button>("Hud/Root/TopBar/Row/Name");
        status = GetNode<Label>("Hud/Root/Status");
        undoButton = GetNode<Button>("Hud/Root/BottomBar/Column/Actions/Undo");
        redoButton = GetNode<Button>("Hud/Root/BottomBar/Column/Actions/Redo");
        playButton = GetNode<Button>("Hud/Root/BottomBar/Column/Actions/Play");
        renameSheet = GetNode<RenameSheet>("Hud/Root/RenameSheet");
        boardSheet = GetNode<BoardSheet>("Hud/Root/BoardSheet");

        GetNode<Button>("Hud/Root/TopBar/Row/Back").Pressed += () => BackPressed?.Invoke();
        nameButton.Pressed += OpenRename;
        GetNode<Button>("Hud/Root/TopBar/Row/More").Pressed += OpenBoardSheet;
        undoButton.Pressed += Undo;
        redoButton.Pressed += Redo;
        playButton.Pressed += OnPlayPressed;
        foreach (var each in Enum.GetValues<EditorTool>())
        {
            var chosen = each;
            ToolButton(chosen).Pressed += () => ChooseTool(chosen);
        }
        input.StrokeMoved += cells => overlay.Cells = [.. cells];
        input.StrokeCancelled += () => overlay.Cells = [];
        input.StrokeEnded += OnStrokeEnded;
        renameSheet.Submitted += OnRenameSubmitted;
        renameSheet.Closed += OnSheetClosed;
        boardSheet.ResizePressed += OnResizePressed;
        boardSheet.ClearConfirmed += OnClearConfirmed;
        boardSheet.Closed += OnSheetClosed;
        topBar.Resized += UpdateInsets;
        bottomBar.Resized += UpdateInsets;
        status.Resized += UpdateInsets;
        GetViewport().SizeChanged += ApplySafeArea;
        board.ThemeChanged += SetClearColor;
        ApplyAppearance();
        ApplySafeArea();
    }

    public void Open(EditSession session, EditorTool tool)
    {
        ArgumentNullException.ThrowIfNull(session);
        this.session = session;
        config = GameConfigFile.Current.CustomLevels;
        rater = new DifficultyRater(GameConfigFile.Current.Difficulty);
        ToolButton(tool).SetPressedNoSignal(true);
        this.tool = tool;
        ShowLevel(reopenCamera: true);
        GD.Print($"Screen: level editor ({session.Level.Name}, {session.Level.Map.Columns}×{session.Level.Map.Rows})");
    }

    public bool CloseOverlay()
    {
        if (renameSheet.IsOpen)
        {
            renameSheet.Close();
            return true;
        }
        if (boardSheet.IsOpen)
        {
            boardSheet.Close();
            return true;
        }
        return false;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (session is null || renameSheet.IsOpen || boardSheet.IsOpen || @event is not InputEventKey { Echo: false } key)
        {
            return;
        }
        if (key.IsActionPressed("undo"))
        {
            GetViewport().SetInputAsHandled();
            Undo();
        }
        else if (key.IsActionPressed("redo"))
        {
            GetViewport().SetInputAsHandled();
            Redo();
        }
    }

    private Button ToolButton(EditorTool each) => GetNode<Button>($"Hud/Root/BottomBar/Column/Tools/{each}");

    private void ChooseTool(EditorTool chosen)
    {
        tool = chosen;
        CancelTeleporter();
        ToolChosen?.Invoke(chosen);
    }

    private void OnStrokeEnded(StrokeEnd end)
    {
        overlay.Cells = [];
        var clock = Stopwatch.StartNew();
        var map = session.Level.Map;
        var first = end.Cells[0];
        EditResult result = tool switch
        {
            EditorTool.Blocks => end.IsTap ? LevelEdits.Split(map, first) : LevelEdits.Paint(map, end.Cells),
            EditorTool.Wall => LevelEdits.PlaceTile(map, end.Cells, Tile.Wall),
            EditorTool.Trap => LevelEdits.PlaceTile(map, end.Cells, Tile.Trap),
            EditorTool.Eraser => LevelEdits.Erase(map, end.Cells),
            EditorTool.Start when end.IsTap => LevelEdits.MoveStart(map, first),
            EditorTool.End when end.IsTap => LevelEdits.MoveEnd(map, first),
            EditorTool.Teleport when end.IsTap => TeleporterTap(map, first),
            _ => null,
        };
        if (result is null)
        {
            return;
        }
        Commit(result, $"{tool}, {Plural(end.Cells.Count, "square")}", clock);
    }

    // The first tap waits for its partner; tapping it again, or another tool, cancels it
    private EditResult TeleporterTap(Map map, Cell cell)
    {
        if (pendingTeleporter is not { } first)
        {
            if (LevelEdits.CanHoldTeleporter(map, cell) is { } refusal)
            {
                return EditResult.Refused(map, refusal);
            }
            pendingTeleporter = cell;
            overlay.Pending = cell;
            ShowStatus();
            return null;
        }
        CancelTeleporter();
        return first == cell ? null : LevelEdits.AddTeleporters(map, first, cell);
    }

    private void CancelTeleporter()
    {
        pendingTeleporter = null;
        overlay.Pending = null;
    }

    // A refusal shows in the status line until the next stroke; a change is written and redrawn, and logged with the time
    // since the caller started its clock
    private void Commit(EditResult result, string name, Stopwatch clock)
    {
        if (result.Refusal is { } refusal)
        {
            ShowStatus(refusal);
            return;
        }
        var sizeBefore = (session.Level.Map.Rows, session.Level.Map.Columns);
        if (session.Apply(result) is not { } level)
        {
            ShowStatus();
            return;
        }
        Changed?.Invoke(level);
        ShowLevel(reopenCamera: (level.Map.Rows, level.Map.Columns) != sizeBefore);
        GD.Print($"Edit: {name}, {clock.ElapsedMilliseconds} ms");
    }

    private void Undo() => Step(() => session.Undo(), "undo");

    private void Redo() => Step(() => session.Redo(), "redo");

    private void Step(Func<CustomLevel> move, string name)
    {
        if (session is null || renameSheet.IsOpen || boardSheet.IsOpen)
        {
            return;
        }
        var clock = Stopwatch.StartNew();
        // A stroke held through the key press was made on the map before the undo, and may not even fit it
        input.Reset();
        CancelTeleporter();
        var sizeBefore = (session.Level.Map.Rows, session.Level.Map.Columns);
        if (move() is not { } level)
        {
            return;
        }
        Changed?.Invoke(level);
        ShowLevel(reopenCamera: (level.Map.Rows, level.Map.Columns) != sizeBefore);
        GD.Print($"Edit: {name}, {clock.ElapsedMilliseconds} ms");
    }

    private void ShowLevel(bool reopenCamera)
    {
        var map = session.Level.Map;
        board.Map = map;
        startMarker.Cell = map.Start;
        player.Cell = map.Start;
        trail.Clear();
        levelStatus = LevelCheck.Of(map, rater, config);
        nameButton.Text = session.Level.Name;
        ShowStatus();
        if (reopenCamera)
        {
            UpdateInsets();
            camera.Open(board.Bounds, board.CellCentre(map.Start));
        }
        board.UpdateView();
    }

    // The level's own line, or in its place a refusal, a failed save, or the pending teleporter's hint, in that order
    private void ShowStatus(string refusal = null)
    {
        var warn = refusal is not null || saveProblem is not null || !levelStatus.IsPlayable;
        status.Text = refusal
            ?? saveProblem
            ?? (pendingTeleporter is not null ? "Tap a square for the other end" : levelStatus.Describe());
        status.ThemeTypeVariation = warn ? "StatusWarn" : "StatusLine";
        undoButton.Disabled = !session.CanUndo;
        redoButton.Disabled = !session.CanRedo;
        playButton.Disabled = !levelStatus.IsPlayable;
    }

    private void OnPlayPressed()
    {
        if (levelStatus.IsPlayable)
        {
            PlayPressed?.Invoke(session.Level);
        }
    }

    private void OpenRename()
    {
        renameSheet.Open(session.Level.Name);
        SheetOpened();
    }

    private void OnRenameSubmitted(string text)
    {
        if (CustomLevelNames.Clean(text, config.MaxNameLength) is not { } name)
        {
            // With no letter limit and no hidden characters, only the byte cap can still refuse it
            var problem = string.IsNullOrWhiteSpace(text) ? "Type a name"
                : CustomLevelNames.HasHiddenCharacters(text) ? "Names can't use those characters"
                : CustomLevelNames.Clean(text, int.MaxValue) is null ? "That name is too long"
                : $"Up to {config.MaxNameLength} letters";
            renameSheet.ShowProblem(problem);
            return;
        }
        if (session.Rename(name) is { } level)
        {
            Changed?.Invoke(level);
            nameButton.Text = level.Name;
            GD.Print($"Edit: renamed to {level.Name}");
        }
        renameSheet.Close();
    }

    private void OpenBoardSheet()
    {
        var map = session.Level.Map;
        boardSheet.Open(map.Rows, map.Columns, config.MinSide, config.MaxSide);
        SheetOpened();
    }

    private void OnResizePressed(int rows, int columns)
    {
        var clock = Stopwatch.StartNew();
        boardSheet.Close();
        Commit(LevelEdits.Resize(session.Level.Map, rows, columns), $"resize to {columns}×{rows}", clock);
    }

    private void OnClearConfirmed()
    {
        var clock = Stopwatch.StartNew();
        boardSheet.Close();
        Commit(LevelEdits.Clear(session.Level.Map), "clear", clock);
    }

    private void SheetOpened()
    {
        CancelTeleporter();
        input.Enabled = false;
        camera.InputEnabled = false;
    }

    private void OnSheetClosed()
    {
        input.Enabled = true;
        camera.InputEnabled = true;
    }

    private void ApplyAppearance()
    {
        if (board is null)
        {
            return;
        }
        var look = Looks.Look(dark);
        board.Theme = Looks.Board(dark);
        startMarker.Look = look;
        trail.Look = look;
        player.Look = look;
        overlay.Color = board.Theme.EditHighlight;
        hudRoot.Theme = Looks.Ui(dark);
    }

    // The status line sits below the top bar, so the board fits below it
    private void UpdateInsets()
    {
        camera.SetInsets(status.GetRect().End.Y, hudRoot.Size.Y - bottomBar.Position.Y);
        backdrop.SetEdges(status.GetRect().End.Y, bottomBar.Position.Y);
        input.BandTop = status.GetRect().End.Y;
        input.BandBottom = bottomBar.Position.Y;
    }

    private void ApplySafeArea()
    {
        var (top, bottom) = SafeArea.Insets(this);
        topBar.OffsetTop = top;
        topBar.OffsetBottom = top + topBar.GetCombinedMinimumSize().Y;
        bottomBar.OffsetBottom = -bottom;
        bottomBar.OffsetTop = -bottom - bottomBar.GetCombinedMinimumSize().Y;
        status.OffsetTop = topBar.OffsetBottom;
        status.OffsetBottom = topBar.OffsetBottom;
        UpdateInsets();
    }

    private void SetClearColor()
    {
        RenderingServer.SetDefaultClearColor(board.Theme.Background);
        backdrop.Color = board.Theme.Background;
    }

    private static string Plural(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";
}
