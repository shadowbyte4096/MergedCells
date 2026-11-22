using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Godot;
using MergedCells.Core;

namespace MergedCells;

// The online level library (layout A, the developer's pick from mockups): newest, most played or most liked, filtered by
// difficulty and size, a page at a time with Load more. Each row is drawn like the custom levels list with how many
// players beat it and liked it, and a tag when the phone holds the level. "Yours" lists the levels the player published.
// Every row is checked on the phone before it shows (LibraryEntries.Check), a few a frame
public partial class LibraryScreen : CanvasLayer, IScreen
{
    // A drawing budget, as CreditsScreen's is: each row is decoded, solved and rated, so a page of big levels is spread
    // over frames
    private const double RowBudgetMs = 4;

    private Control root;
    private Control topBar;
    private Control bottomPad;
    private Label title;
    private Button yours;
    private Control sortRow;
    private Control filterRow;
    private Button newest;
    private Button played;
    private Button liked;
    private Button difficulty;
    private Button size;
    private VBoxContainer list;
    private Label state;
    private Button retry;
    private Button more;
    private Button unblock;
    private LibrarySheet levelSheet;
    private ReportSheet reportSheet;
    private ChoiceSheet choiceSheet;
    private ImportSheet importSheet;

    private ICallSender sender;
    private CustomLevelsConfig customLevels;
    private LibraryConfig library;
    private DifficultyRater rater;
    private Func<CustomLevelLoad> load;
    private CustomLevelLoad held = new([], [], new HashSet<Guid>());
    private LibraryQuery query = new(LibraryOrder.Newest);
    private bool mine;
    private bool choosingSize;

    private CallHandle<LibraryPage> page;
    private CallHandle<BlockReply> unblocking;
    private int pageIndex;
    private bool fullPage;
    private bool footerDue;
    private int blocked;
    private bool unblockArmed;
    private readonly Queue<LibraryRow> toCheck = new();
    private readonly HashSet<Guid> shown = new();
    private readonly List<LibraryEntry> entries = new();
    private readonly List<LevelPictureView> pictures = new();
    private LibraryEntry selected;
    private CustomLevel gotten;
    private Vector2 pressedAt;
    private bool dragged;
    private bool dark;

    public event Action BackPressed;

    public event Action<LibraryQuery> QueryChanged;

    public event Action<LibraryEntry> GetPressed;

    public event Action<CustomLevel> PlayPressed;

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
        title = GetNode<Label>("Root/Layout/TopBar/Row/Title");
        yours = GetNode<Button>("Root/Layout/TopBar/Row/Yours");
        sortRow = GetNode<Control>("Root/Layout/SortRow");
        filterRow = GetNode<Control>("Root/Layout/FilterRow");
        newest = GetNode<Button>("Root/Layout/SortRow/Switch/Newest");
        played = GetNode<Button>("Root/Layout/SortRow/Switch/Played");
        liked = GetNode<Button>("Root/Layout/SortRow/Switch/Liked");
        difficulty = GetNode<Button>("Root/Layout/FilterRow/Filters/Difficulty");
        size = GetNode<Button>("Root/Layout/FilterRow/Filters/Size");
        list = GetNode<VBoxContainer>("Root/Layout/Scroll/Margin/Column/List");
        state = GetNode<Label>("Root/Layout/Scroll/Margin/Column/State");
        retry = GetNode<Button>("Root/Layout/Scroll/Margin/Column/Retry");
        more = GetNode<Button>("Root/Layout/Scroll/Margin/Column/More");
        unblock = GetNode<Button>("Root/Layout/Scroll/Margin/Column/Unblock");
        levelSheet = GetNode<LibrarySheet>("Root/LevelSheet");
        reportSheet = GetNode<ReportSheet>("Root/ReportSheet");
        choiceSheet = GetNode<ChoiceSheet>("Root/ChoiceSheet");
        importSheet = GetNode<ImportSheet>("Root/ImportSheet");

        GetNode<Button>("Root/Layout/TopBar/Row/Back").Pressed += () =>
        {
            if (!LeaveYours())
            {
                BackPressed?.Invoke();
            }
        };
        yours.Pressed += () =>
        {
            mine = true;
            UpdateControls();
            LoadPage(more: false);
        };
        newest.Pressed += () => SetQuery(query with { Order = LibraryOrder.Newest });
        played.Pressed += () => SetQuery(query with { Order = LibraryOrder.Played });
        liked.Pressed += () => SetQuery(query with { Order = LibraryOrder.Liked });
        difficulty.Pressed += OpenDifficulty;
        size.Pressed += OpenSize;
        choiceSheet.Chosen += index =>
        {
            int? chosen = index == 0 ? null : index - 1;
            SetQuery(choosingSize ? query with { SizeStep = chosen } : query with { Band = chosen });
        };
        retry.Pressed += () => LoadPage(more: false);
        more.Pressed += () => LoadPage(more: true);
        unblock.Pressed += OnUnblockPressed;
        GetNode<Button>("Root/Layout/Scroll/Margin/Column/Discord").Pressed += () => OS.ShellOpen(Community.DiscordUrl);

        levelSheet.GetPressed += () => GetPressed?.Invoke(selected);
        levelSheet.ReportPressed += () =>
        {
            levelSheet.Close();
            reportSheet.Open(selected.Level?.Name ?? "this level");
        };
        levelSheet.BlockConfirmed += () =>
        {
            var call = sender.Send(LibraryCalls.Block(selected.Id));
            levelSheet.Watch("Blocking…", () => LibraryLines.Block(call));
        };
        levelSheet.RemoveConfirmed += () =>
        {
            var call = sender.Send(LibraryCalls.Remove(selected.Id));
            levelSheet.Watch("Removing…", () => LibraryLines.Remove(call));
        };
        // A block hides more than this row, and a remove changes "Yours": the list loads again under the sheet
        levelSheet.Settled += () => LoadPage(more: false);
        reportSheet.Chosen += reason =>
        {
            var call = sender.Send(LibraryCalls.Report(selected.Id, reason));
            reportSheet.Watch(() => LibraryLines.Report(call));
        };
        importSheet.PlayPressed += () => PlayPressed?.Invoke(gotten);
        importSheet.Closed += () =>
        {
            held = load();
            RebuildRows();
        };
        GetViewport().SizeChanged += ApplySafeArea;
        SetProcess(false);
        ApplyLook();
        ApplySafeArea();
    }

    /// <summary>
    /// Opens on the order and filters last used this run. With no server for the run (<paramref name="on"/> false) it says
    /// the library isn't available and sends nothing.
    /// </summary>
    public void Open(
        ICallSender sender, LibraryQuery query, CustomLevelsConfig customLevels, LibraryConfig library, DifficultyRater rater,
        Func<CustomLevelLoad> load, bool on)
    {
        this.sender = sender;
        this.query = query;
        this.customLevels = customLevels;
        this.library = library;
        this.rater = rater;
        this.load = load;
        held = load();
        UpdateControls();
        if (!on)
        {
            ShowOff();
            return;
        }
        LoadPage(more: false);
    }

    // Back from "Yours" goes to browsing; false when browsing already
    public bool LeaveYours()
    {
        if (!mine)
        {
            return false;
        }
        mine = false;
        UpdateControls();
        LoadPage(more: false);
        return true;
    }

    // What Get did, in chunk 21d's import sheet over the library, with Play for a level that was written
    public void ShowImport(ImportResult result)
    {
        gotten = result.Level;
        PanelContainer row = null;
        if (result.Level is { } level)
        {
            var status = LevelCheck.Of(level.Map, rater, customLevels);
            row = RowPanel("SheetRowPanel", level.Map, level.Name, level.IsImported ? "Imported" : null, false, Meta(level, status), null);
        }
        levelSheet.Close();
        importSheet.Open(result.Title, row, result.Describe(customLevels.MaxSide));
    }

    public bool CloseOverlay()
    {
        var open = levelSheet.IsOpen || reportSheet.IsOpen || choiceSheet.IsOpen || importSheet.IsOpen;
        levelSheet.Close();
        reportSheet.Close();
        choiceSheet.Close();
        importSheet.Close();
        return open;
    }

    // Leaving before the answer: a page still waiting is never sent
    public override void _ExitTree() => page?.Cancel();

    // Read before the rows, so a touch that scrolled the list past the swipe distance doesn't also open the row it ends on
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

    // Polled rather than subscribed: the screen can go before the answer, and an event would then call into a freed node
    public override void _Process(double delta)
    {
        if (page is { State: not CallState.Waiting } answered)
        {
            page = null;
            OnPage(answered);
        }
        if (unblocking is { State: not CallState.Waiting })
        {
            unblocking = null;
            LoadPage(more: false);
        }
        var clock = Stopwatch.StartNew();
        while (toCheck.Count > 0 && clock.Elapsed.TotalMilliseconds < RowBudgetMs)
        {
            if (LibraryEntries.Check(toCheck.Dequeue(), rater, customLevels) is { } entry)
            {
                entries.Add(entry);
                AddRow(entry);
            }
        }
        if (footerDue && page is null && toCheck.Count == 0)
        {
            footerDue = false;
            ShowFooter();
        }
        if (page is null && unblocking is null && toCheck.Count == 0)
        {
            SetProcess(false);
        }
    }

    private void Down(Vector2 at)
    {
        pressedAt = at;
        dragged = false;
    }

    private void Moved(Vector2 at) => dragged = dragged || pressedAt.DistanceTo(at) > GameConfigFile.Current.Input.SwipeThreshold;

    private void SetQuery(LibraryQuery next)
    {
        query = next;
        QueryChanged?.Invoke(query);
        UpdateControls();
        LoadPage(more: false);
    }

    private void LoadPage(bool more)
    {
        page?.Cancel();
        if (more)
        {
            pageIndex++;
        }
        else
        {
            pageIndex = 0;
            entries.Clear();
            shown.Clear();
            toCheck.Clear();
            ClearRows();
        }
        this.more.Hide();
        retry.Hide();
        unblock.Hide();
        ShowState("Loading…");
        page = sender.Send(mine ? LibraryCalls.Mine() : LibraryCalls.Levels(query, pageIndex, library, customLevels));
        footerDue = false;
        SetProcess(true);
    }

    private void OnPage(CallHandle<LibraryPage> answered)
    {
        switch (answered.State)
        {
            case CallState.Answered:
                var rows = answered.Answer!.Rows;
                blocked = answered.Answer.Blocked;
                fullPage = !mine && rows.Count >= library.PageSize;
                foreach (var row in LibraryEntries.Unseen(shown, rows))
                {
                    shown.Add(row.Id);
                    toCheck.Enqueue(row);
                }
                state.Hide();
                footerDue = true;
                GD.Print($"Library: {rows.Count} levels in page {pageIndex}");
                break;
            case CallState.Unavailable:
                ShowState("Can't reach the library right now.");
                retry.Show();
                break;
            default:
                ShowOff();
                break;
        }
    }

    private void ShowFooter()
    {
        if (entries.Count == 0)
        {
            ShowState(mine ? "You haven't published any levels yet." : "No levels here yet.");
        }
        more.Visible = fullPage;
        unblockArmed = false;
        unblock.Text = string.Create(CultureInfo.InvariantCulture, $"Unblock everyone ({blocked})");
        unblock.Visible = !mine && blocked > 0;
        unblock.Disabled = false;
    }

    private void ShowOff()
    {
        ClearRows();
        sortRow.Hide();
        filterRow.Hide();
        yours.Hide();
        ShowState("The library isn't available right now.");
    }

    private void ShowState(string sentence)
    {
        state.Text = sentence;
        state.Show();
    }

    private void OnUnblockPressed()
    {
        if (!unblockArmed)
        {
            unblockArmed = true;
            unblock.Text = "Tap again to unblock everyone";
            return;
        }
        unblockArmed = false;
        unblock.Disabled = true;
        unblocking = sender.Send(LibraryCalls.Unblock());
        SetProcess(true);
    }

    private void UpdateControls()
    {
        title.Text = mine ? "Your levels" : "Library";
        yours.Visible = !mine;
        sortRow.Visible = !mine;
        filterRow.Visible = !mine;
        newest.SetPressedNoSignal(query.Order == LibraryOrder.Newest);
        played.SetPressedNoSignal(query.Order == LibraryOrder.Played);
        liked.SetPressedNoSignal(query.Order == LibraryOrder.Liked);
        difficulty.Text = query.Band is { } band ? customLevels.Bands[band].Name : "Any difficulty";
        size.Text = query.SizeStep is { } step ? SizeName(step) : "Any size";
        unblock.Disabled = false;
    }

    private void OpenDifficulty()
    {
        choosingSize = false;
        var options = new List<string> { "Any difficulty" };
        options.AddRange(customLevels.Bands.Select(band => band.Name));
        choiceSheet.Open("Difficulty", options, query.Band is { } band ? band + 1 : 0);
    }

    private void OpenSize()
    {
        choosingSize = true;
        var options = new List<string> { "Any size" };
        options.AddRange(Enumerable.Range(0, library.SizeSteps.Count + 1).Select(SizeName));
        choiceSheet.Open("Size", options, query.SizeStep is { } step ? step + 1 : 0);
    }

    private string SizeName(int step)
    {
        var (min, max) = new LibraryQuery(LibraryOrder.Newest, SizeStep: step).SideRange(library, customLevels)!.Value;
        return min == 1
            ? string.Create(CultureInfo.InvariantCulture, $"Up to {max}")
            : string.Create(CultureInfo.InvariantCulture, $"{min}–{max}");
    }

    private void ClearRows()
    {
        foreach (var child in list.GetChildren())
        {
            child.QueueFree();
        }
    }

    // After a Get the phone holds more levels, so the tags are worked out again without asking the server
    private void RebuildRows()
    {
        ClearRows();
        foreach (var entry in entries)
        {
            AddRow(entry);
        }
    }

    private void AddRow(LibraryEntry entry)
    {
        var local = held.Levels.FirstOrDefault(level => level.Id == entry.Id);
        var (tag, get) = LibraryTags.For(entry, held);
        var level = entry.Level ?? local;
        var name = level?.Name ?? "Removed level";
        var meta = entry.Level is { } shownLevel ? Meta(shownLevel, entry.Status!) : "Removed from the library";
        var stats = entry.Removed ? null
            : entry.Hidden ? "Hidden after reports, waiting to be checked"
            : Stats(entry.Finished, entry.Likes);
        var tagText = tag switch
        {
            LibraryTag.Yours => "Yours",
            LibraryTag.InYourLevels => "In your levels",
            LibraryTag.Update => "Update",
            _ => null,
        };
        var panel = RowPanel("ListRowPanel", level?.Map, name, tagText, tag == LibraryTag.Yours, meta, stats);
        list.AddChild(LevelRows.Pressable(panel, () =>
        {
            selected = entry;
            var picture = level is null ? null : new LevelPictureView { Map = level.Map, Board = Looks.Board(dark) };
            if (picture is not null)
            {
                pictures.Add(picture);
            }
            levelSheet.Open(entry, picture, name, entry.Level is null ? "" : meta, stats ?? meta, get);
        }, () => dragged));
    }

    private PanelContainer RowPanel(string variation, Map map, string name, string tag, bool solidTag, string meta, string stats) =>
        LevelRows.Panel(variation, map, Looks.Board(dark), pictures, name, tag, meta, false, stats, solidTag);

    private static string Meta(CustomLevel level, LevelStatus status) =>
        string.Create(CultureInfo.InvariantCulture, $"{level.Map.Columns}×{level.Map.Rows} · {status.Describe()}");

    private static string Stats(int finished, int likes)
    {
        if (finished == 0)
        {
            return "Nobody has beaten it yet";
        }
        var likesText = likes == 1 ? "1 like" : string.Create(CultureInfo.InvariantCulture, $"{likes} likes");
        return string.Create(CultureInfo.InvariantCulture, $"{finished} beat it · {likesText}");
    }

    private void ApplyLook()
    {
        if (root is null)
        {
            return;
        }
        root.Theme = Looks.Ui(dark);
        RenderingServer.SetDefaultClearColor(Looks.Board(dark).Background);
        // Sheets and rebuilt lists free their old pictures
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
