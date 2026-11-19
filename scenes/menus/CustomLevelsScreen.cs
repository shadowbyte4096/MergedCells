using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Godot;
using MergedCells.Core;

namespace MergedCells;

// A campaign level the player can copy into the editor, with its chapter and number in the chapter for the picker
public sealed record CopyChoice(Chapter Chapter, int Number, Level Level);

// The player's custom levels, newest first (an imported level by when it was imported), each with its picture, size and
// par and band, or why it can't be played yet. "+ New level" picks a size, or a 3-starred campaign level to copy, and
// opens the editor; Import reads a share code from the clipboard; a row opens its actions
public partial class CustomLevelsScreen : CanvasLayer, IScreen
{
    private Control root;
    private Control topBar;
    private Control bottomPad;
    private VBoxContainer list;
    private Label empty;
    private Label notice;
    private BoardSheet newSheet;
    private Button copyButton;
    private Label copyNote;
    private LevelSheet levelSheet;
    private ImportSheet importSheet;
    private CopySheet copySheet;
    private CustomLevelsConfig config;
    private IReadOnlyList<CopyChoice> copyable = [];
    private CustomLevel selected;
    private CustomLevel imported;
    private Vector2 pressedAt;
    private bool dragged;
    private bool dark;
    private readonly List<LevelPictureView> pictures = new();

    public event Action BackPressed;

    public event Action<int, int> NewPressed;

    public event Action ImportPressed;

    // An imported level too: Main makes it a copy
    public event Action<CustomLevel> EditPressed;

    // The player's own level: an imported one's copy comes through EditPressed
    public event Action<CustomLevel> EditCopyPressed;

    public event Action<CustomLevel> PlayPressed;

    public event Action<CustomLevel> SharePressed;

    public event Action<CustomLevel> DeletePressed;

    public event Action<Level> CopyChosen;

    [Export] public AppLooks Looks { get; set; }

    public bool Dark
    {
        set
        {
            dark = value;
            ApplyLook();
        }
    }

    public override void _Ready()
    {
        root = GetNode<Control>("Root");
        topBar = GetNode<Control>("Root/Layout/TopBar");
        bottomPad = GetNode<Control>("Root/Layout/BottomPad");
        list = GetNode<VBoxContainer>("Root/Layout/Scroll/Margin/List");
        empty = GetNode<Label>("Root/Layout/Scroll/Margin/List/Empty");
        notice = GetNode<Label>("Root/Layout/Notice");
        newSheet = GetNode<BoardSheet>("Root/NewSheet");
        copyButton = GetNode<Button>("Root/NewSheet/Sheet/Column/Copy");
        copyNote = GetNode<Label>("Root/NewSheet/Sheet/Column/CopyNote");
        levelSheet = GetNode<LevelSheet>("Root/LevelSheet");
        importSheet = GetNode<ImportSheet>("Root/ImportSheet");
        copySheet = GetNode<CopySheet>("Root/CopySheet");

        GetNode<Button>("Root/Layout/TopBar/Row/Back").Pressed += () => BackPressed?.Invoke();
        GetNode<Button>("Root/Layout/NewRow/Buttons/New").Pressed += OpenNewSheet;
        GetNode<Button>("Root/Layout/NewRow/Buttons/Import").Pressed += () => ImportPressed?.Invoke();
        newSheet.ResizePressed += (rows, columns) =>
        {
            newSheet.Close();
            NewPressed?.Invoke(rows, columns);
        };
        copyButton.Pressed += OpenCopySheet;
        levelSheet.PlayPressed += () => PlayPressed?.Invoke(selected);
        levelSheet.SharePressed += () => SharePressed?.Invoke(selected);
        levelSheet.EditPressed += () => EditPressed?.Invoke(selected);
        levelSheet.EditCopyPressed += () => EditCopyPressed?.Invoke(selected);
        levelSheet.DeleteConfirmed += () => DeletePressed?.Invoke(selected);
        importSheet.PlayPressed += () => PlayPressed?.Invoke(imported);
        GetViewport().SizeChanged += ApplySafeArea;
        ApplyLook();
        ApplySafeArea();
    }

    /// <summary>
    /// Reads and checks every level each time it opens, so the list always matches the files. <paramref name="shown"/> is
    /// an import just made, whose sheet opens over the list.
    /// </summary>
    public void Open(
        CustomLevelLoad load, DifficultyRater rater, CustomLevelsConfig config, string notice, IReadOnlyList<CopyChoice> copyable,
        ImportResult shown = null)
    {
        this.config = config;
        this.copyable = copyable;
        var clock = Stopwatch.StartNew();
        foreach (var level in load.Levels)
        {
            AddRow(level, LevelCheck.Of(level.Map, rater, config));
        }
        empty.Visible = load.Levels.Count == 0;
        this.notice.Text = notice ?? "";
        this.notice.Visible = notice != null;
        var count = load.Levels.Count == 1 ? "1 level" : $"{load.Levels.Count} levels";
        GD.Print($"Screen: custom levels ({count} checked in {clock.ElapsedMilliseconds} ms)");
        if (shown is not null)
        {
            imported = shown.Level;
            var row = shown.Level is { } level
                ? RowPanel("SheetRowPanel", level.Map, level.Name, level.IsImported, Line(level.Map, LevelCheck.Of(level.Map, rater, config)), false)
                : null;
            importSheet.Open(shown.Title, row, shown.Describe(config.MaxSide));
        }
    }

    public void ShowCopied() => levelSheet.ShowCopied();

    // Read before the rows, so a touch that scrolled the list past the swipe distance doesn't also open the row it ends on.
    // Godot sends the button its own emulated mouse release, so marking the touch handled wouldn't stop it
    public override void _Input(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventScreenTouch { Pressed: true } touch:
                Down(touch.Position);
                break;
            case InputEventScreenDrag drag:
                Moved(drag.Position);
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } press
                when press.Device != InputEvent.DeviceIdEmulation:
                Down(press.Position);
                break;
            case InputEventMouseMotion motion
                when motion.Device != InputEvent.DeviceIdEmulation && motion.ButtonMask.HasFlag(MouseButtonMask.Left):
                Moved(motion.Position);
                break;
        }
    }

    private void Down(Vector2 at)
    {
        pressedAt = at;
        dragged = false;
    }

    private void Moved(Vector2 at) => dragged = dragged || pressedAt.DistanceTo(at) > GameConfigFile.Current.Input.SwipeThreshold;

    public bool CloseOverlay()
    {
        var open = newSheet.IsOpen || levelSheet.IsOpen || importSheet.IsOpen || copySheet.IsOpen;
        newSheet.Close();
        levelSheet.Close();
        importSheet.Close();
        copySheet.Close();
        return open;
    }

    private static string Line(Map map, LevelStatus status) =>
        string.Create(CultureInfo.InvariantCulture, $"{map.Columns}×{map.Rows} · {status.Describe()}");

    private void AddRow(CustomLevel level, LevelStatus status)
    {
        var line = Line(level.Map, status);
        list.AddChild(Pressable(
            RowPanel("ListRowPanel", level.Map, level.Name, level.IsImported, line, !status.IsPlayable),
            () =>
            {
                selected = level;
                levelSheet.Open(level.Name, line, status.IsPlayable, status.IsPlayable && ShareCode.CanWrite(level), level.IsImported);
            }));
    }

    // A see-through button over the whole row takes the tap. It passes presses on, so the scroll container can drag, and
    // ignores a press at the end of a drag (see _Input)
    private MarginContainer Pressable(Control panel, Action pressed)
    {
        var row = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddChild(panel);
        var button = new Button
        {
            ThemeTypeVariation = "ListRow",
            FocusMode = Control.FocusModeEnum.None,
            MouseFilter = Control.MouseFilterEnum.Pass,
        };
        button.Pressed += () =>
        {
            if (!dragged)
            {
                pressed();
            }
        };
        row.AddChild(button);
        return row;
    }

    // A level's picture, its name (with the Imported tag) and a line under it, in a row panel. The tag sits at the end of
    // the name's line: Godot's HBox can't keep it hugging a name that has to shrink and ellipsize
    private PanelContainer RowPanel(string variation, Map map, string name, bool isImported, string line, bool warn)
    {
        var panel = new PanelContainer { ThemeTypeVariation = variation, MouseFilter = Control.MouseFilterEnum.Ignore };
        var content = new HBoxContainer { ThemeTypeVariation = "PictureRow", MouseFilter = Control.MouseFilterEnum.Ignore };
        var picture = new LevelPictureView
        {
            Map = map,
            Board = Looks.Board(dark),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        pictures.Add(picture);
        content.AddChild(picture);
        var column = new VBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        var nameRow = new HBoxContainer { ThemeTypeVariation = "NameRow", MouseFilter = Control.MouseFilterEnum.Ignore };
        nameRow.AddChild(new Label
        {
            Text = name,
            ThemeTypeVariation = "RowName",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            ClipText = true,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
        });
        if (isImported)
        {
            nameRow.AddChild(new Label
            {
                Text = "Imported",
                ThemeTypeVariation = "ImportedTag",
                MouseFilter = Control.MouseFilterEnum.Ignore,
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            });
        }
        column.AddChild(nameRow);
        column.AddChild(new Label
        {
            Text = line,
            ThemeTypeVariation = warn ? "RowMetaWarn" : "RowMeta",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ClipText = true,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
        });
        content.AddChild(column);
        panel.AddChild(content);
        return panel;
    }

    private void OpenNewSheet()
    {
        copyButton.Disabled = copyable.Count == 0;
        copyNote.Visible = copyable.Count == 0;
        newSheet.Open(config.NewSide, config.NewSide, config.MinSide, config.MaxSide);
    }

    // Closes the New level sheet first, so Back from the picker goes to the list
    private void OpenCopySheet()
    {
        newSheet.Close();
        var items = new List<Control>();
        Chapter chapter = null;
        foreach (var choice in copyable)
        {
            if (choice.Chapter != chapter)
            {
                chapter = choice.Chapter;
                items.Add(new Label
                {
                    Text = string.Create(CultureInfo.InvariantCulture, $"Chapter {chapter.Number} · {chapter.Name}"),
                    ThemeTypeVariation = "Muted",
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                });
            }
            var level = choice.Level;
            var name = string.Create(CultureInfo.InvariantCulture, $"{choice.Number} · {level.Name}");
            var line = string.Create(CultureInfo.InvariantCulture, $"{level.Map.Columns}×{level.Map.Rows} · Par {level.Par}");
            items.Add(Pressable(RowPanel("SheetRowPanel", level.Map, name, false, line, false), () => CopyChosen?.Invoke(level)));
        }
        copySheet.Open(items);
    }

    private void ApplyLook()
    {
        if (root is null)
        {
            return;
        }
        root.Theme = Looks.Ui(dark);
        RenderingServer.SetDefaultClearColor(Looks.Board(dark).Background);
        // Sheets free their old rows when they reopen
        pictures.RemoveAll(picture => !IsInstanceValid(picture));
        foreach (var picture in pictures)
        {
            picture.Board = Looks.Board(dark);
        }
    }

    private void ApplySafeArea()
    {
        var (top, bottom) = SafeArea.Insets(this);
        topBar.GetParent<Control>().OffsetTop = top;
        bottomPad.CustomMinimumSize = new Vector2(0, bottom);
    }
}
