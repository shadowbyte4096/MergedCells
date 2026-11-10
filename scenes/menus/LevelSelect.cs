using System;
using System.Linq;
using Godot;
using MergedCells.Core;

namespace MergedCells;

// One open chapter per page: its levels as tiles, the star total, and on the last open chapter's page the next locked
// chapter's card. The chevrons, Left and Right, or a sideways swipe move between open chapters.
public partial class LevelSelect : CanvasLayer, IScreen
{
    private Control root;
    private Control layout;
    private Label totalLabel;
    private Button previous;
    private Button next;
    private Label chapterLabel;
    private Label nameLabel;
    private GridContainer grid;
    private Control card;
    private Label cardTitle;
    private Label cardLine;
    private PageDots dots;
    private SettingsSheet settings;
    private SwipeDetector swipes;
    private Campaign campaign;
    private CampaignProgress progress;
    private Chapter chapter;
    private Appearance appearance;
    private bool dark;

    // Set by a gesture that crossed the swipe threshold, so the tile it started on doesn't open on release. Godot sends
    // the tile its own emulated mouse release, so marking the touch handled wouldn't stop it.
    private bool swiped;

    public event Action<Level> LevelChosen;

    public event Action BackPressed;

    public event Action<Appearance> AppearanceChosen;

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
        layout = GetNode<Control>("Root/Layout");
        totalLabel = GetNode<Label>("Root/Layout/TopBar/Row/Total/TotalLabel");
        previous = GetNode<Button>("Root/Layout/Header/Row/Previous");
        next = GetNode<Button>("Root/Layout/Header/Row/Next");
        chapterLabel = GetNode<Label>("Root/Layout/Header/Row/Heading/ChapterLabel");
        nameLabel = GetNode<Label>("Root/Layout/Header/Row/Heading/NameLabel");
        grid = GetNode<GridContainer>("Root/Layout/Middle/Column/GridCentre/Grid");
        card = GetNode<Control>("Root/Layout/Middle/Column/CardCentre/LockedCard");
        cardTitle = GetNode<Label>("Root/Layout/Middle/Column/CardCentre/LockedCard/Column/CardTitle");
        cardLine = GetNode<Label>("Root/Layout/Middle/Column/CardCentre/LockedCard/Column/CardLine");
        dots = GetNode<PageDots>("Root/Layout/Dots");
        settings = GetNode<SettingsSheet>("Root/Settings");
        swipes = new SwipeDetector(GameConfigFile.Current.Input.SwipeThreshold);

        GetNode<StarsView>("Root/Layout/TopBar/Row/Total/Star").Looks = [StarLook.Earned];
        // Every button here ignores a press at the end of a swipe, not only the tiles: the buttons are 48 units wide and a
        // swipe fires at 40, so one can start and end on a button
        GetNode<Button>("Root/Layout/TopBar/Row/Back").Pressed += () => Unswiped(() => BackPressed?.Invoke());
        GetNode<Button>("Root/Layout/TopBar/Row/SettingsButton").Pressed += () => Unswiped(() => settings.Open(appearance));
        previous.Pressed += () => Unswiped(() => Page(-1));
        next.Pressed += () => Unswiped(() => Page(1));
        settings.AppearanceChosen += choice =>
        {
            appearance = choice;
            AppearanceChosen?.Invoke(choice);
        };
        GetViewport().SizeChanged += ApplySafeArea;
        ApplyLook();
        ApplySafeArea();
    }

    public void Open(Campaign shown, CampaignProgress current, Chapter page, Appearance saved)
    {
        campaign = shown;
        progress = current;
        appearance = saved;
        totalLabel.Text = progress.TotalStars.ToString();
        ShowChapter(page);
    }

    public bool CloseOverlay()
    {
        var open = settings.IsOpen;
        settings.Close();
        return open;
    }

    public override void _Input(InputEvent @event)
    {
        if (settings is null || settings.IsOpen)
        {
            return;
        }
        switch (@event)
        {
            case InputEventScreenTouch touch:
                if (touch.Pressed)
                {
                    swiped = false;
                    swipes.Press(touch.Index, touch.Position.X, touch.Position.Y);
                }
                else
                {
                    swipes.Release(touch.Index);
                }
                break;
            // A real mouse press starts a new gesture too. Touches' emulated presses arrive with their touch, already cleared.
            case InputEventMouseButton { Pressed: true } click when click.Device != InputEvent.DeviceIdEmulation:
                swiped = false;
                break;
            case InputEventScreenDrag drag:
                if (swipes.Drag(drag.Index, drag.Position.X, drag.Position.Y) is { } direction)
                {
                    swiped = true;
                    if (direction == Direction.Left)
                    {
                        Page(1);
                    }
                    else if (direction == Direction.Right)
                    {
                        Page(-1);
                    }
                }
                break;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Echo: false } key || settings.IsOpen)
        {
            return;
        }
        if (key.IsActionPressed("move_left"))
        {
            GetViewport().SetInputAsHandled();
            Page(-1);
        }
        else if (key.IsActionPressed("move_right"))
        {
            GetViewport().SetInputAsHandled();
            Page(1);
        }
    }

    public override void _Notification(int what)
    {
        // The OS may never send the releases for touches held while the app loses focus
        if (what == NotificationApplicationFocusOut || what == NotificationApplicationPaused)
        {
            swipes?.Reset();
        }
    }

    private void Page(int step)
    {
        if (progress is null)
        {
            return;
        }
        var open = progress.OpenChapters;
        var index = IndexOf(chapter) + step;
        if (index >= 0 && index < open.Count)
        {
            ShowChapter(open[index]);
        }
    }

    private int IndexOf(Chapter page) => progress.OpenChapters.ToList().FindIndex(open => open.Number == page.Number);

    private void ShowChapter(Chapter page)
    {
        chapter = page;
        var open = progress.OpenChapters;
        var index = IndexOf(page);
        chapterLabel.Text = $"Chapter {page.Number}";
        nameLabel.Text = page.Name;
        // Hidden but still taking their space, so the heading stays centred
        SetChevron(previous, index > 0);
        SetChevron(next, index + 1 < open.Count);

        foreach (var old in grid.GetChildren())
        {
            grid.RemoveChild(old);
            old.QueueFree();
        }
        var current = progress.Current(page);
        for (var i = 0; i < page.Levels.Count; i++)
        {
            var level = page.Levels[i];
            var tile = new LevelTile();
            grid.AddChild(tile);
            tile.Set(i + 1, progress.Stars(level), current?.Id == level.Id);
            tile.Pressed += () => OnTilePressed(level);
        }

        // Always three columns wide, so a short chapter's tiles line up from the left as a full one's do
        var gridWidth = grid.Columns * grid.GetThemeConstant("width", "LevelTile")
            + (grid.Columns - 1) * grid.GetThemeConstant("h_separation");
        grid.CustomMinimumSize = new Vector2(gridWidth, 0);

        var lastOpen = index == open.Count - 1;
        card.Visible = lastOpen && page.Number < campaign.Chapters.Count;
        if (card.Visible)
        {
            var locked = campaign.Chapters[page.Number];
            cardTitle.Text = $"Chapter {locked.Number} · {locked.Name}";
            var gate = progress.Gate(locked);
            cardLine.Text = gate == 1 ? "1 star to open" : $"{gate} stars to open";
            card.CustomMinimumSize = new Vector2(gridWidth, 0);
        }
        dots.Visible = open.Count > 1;
        dots.Set(open.Count, index);
        GD.Print($"Screen: chapter {page.Number} ({page.Name}), {progress.TotalStars} stars");
    }

    private static void SetChevron(Button chevron, bool shown)
    {
        chevron.Disabled = !shown;
        chevron.Modulate = shown ? Colors.White : Colors.Transparent;
    }

    private void OnTilePressed(Level level)
    {
        if (!settings.IsOpen)
        {
            Unswiped(() => LevelChosen?.Invoke(level));
        }
    }

    private void Unswiped(Action press)
    {
        if (!swiped)
        {
            press();
        }
    }

    private void ApplyLook()
    {
        if (root is null)
        {
            return;
        }
        root.Theme = Looks.Ui(dark);
        RenderingServer.SetDefaultClearColor(Looks.Board(dark).Background);
    }

    private void ApplySafeArea()
    {
        var (top, bottom) = SafeArea.Insets(this);
        layout.OffsetTop = top;
        layout.OffsetBottom = -bottom;
    }
}
