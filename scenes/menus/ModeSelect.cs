using System;
using Godot;
using MergedCells.Core;

namespace MergedCells;

// The first screen: the title, the star total, Campaign, Custom levels, Infinite, the modes still to come greyed out, Credits and the gear
public partial class ModeSelect : CanvasLayer, IScreen
{
    private Control root;
    private Control topBar;
    private Control bottomBar;
    private StarsView star;
    private Label totalLabel;
    private Button testBoards;
    private Button resetSave;
    private bool resetArmed;
    private Label saveNotice;
    private SettingsSheet settings;
    private ConsentSheet consent;
    private bool dark;

    public event Action CampaignPressed;

    public event Action CustomLevelsPressed;

    public event Action InfinitePressed;

    public event Action CreditsPressed;

    public event Action TestBoardsPressed;

    // Debug builds only, after a second tap: the first only asks
    public event Action ResetSaveConfirmed;

    [Export] public AppLooks Looks { get; set; }

    public SettingsSheet Settings => settings;

    // The one-time ask about play data and, from chunk 33, leaderboards; Main decides what it asks
    public ConsentSheet Consent => consent;

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
        topBar = GetNode<Control>("Root/TopBar");
        bottomBar = GetNode<Control>("Root/BottomBar");
        star = GetNode<StarsView>("Root/Centre/Column/Total/Star");
        totalLabel = GetNode<Label>("Root/Centre/Column/Total/TotalLabel");
        testBoards = GetNode<Button>("Root/Centre/Column/TestBoards");
        resetSave = GetNode<Button>("Root/Centre/Column/ResetSave");
        saveNotice = GetNode<Label>("Root/Centre/Column/SaveNotice");
        settings = GetNode<SettingsSheet>("Root/Settings");
        consent = GetNode<ConsentSheet>("Root/Consent");

        star.Looks = [StarLook.Earned];
        GetNode<Button>("Root/Centre/Column/Campaign").Pressed += () => CampaignPressed?.Invoke();
        GetNode<Button>("Root/Centre/Column/CustomLevels").Pressed += () => CustomLevelsPressed?.Invoke();
        testBoards.Pressed += () => TestBoardsPressed?.Invoke();
        resetSave.Pressed += OnResetSavePressed;
        GetNode<Button>("Root/BottomBar/Credits").Pressed += () => CreditsPressed?.Invoke();
        GetNode<Button>("Root/Centre/Column/Infinite").Pressed += () => InfinitePressed?.Invoke();
        GetNode<Button>("Root/TopBar/Row/SettingsButton").Pressed += () => settings.Open();
        GetViewport().SizeChanged += ApplySafeArea;
        ApplyLook();
        ApplySafeArea();
    }

    // debug shows Test boards and Reset save. saveNotice: why this run's progress isn't being kept, or null while saving works
    public void Open(int totalStars, bool debug, string saveNotice)
    {
        totalLabel.Text = totalStars.ToString();
        testBoards.Visible = debug;
        resetSave.Visible = debug;
        ShowSaveNotice(saveNotice);
        GD.Print("Screen: mode select");
    }

    // Also after an answer to the ask that the save couldn't keep
    public void ShowSaveNotice(string notice)
    {
        saveNotice.Text = notice ?? "";
        saveNotice.Visible = notice != null;
    }

    private void OnResetSavePressed()
    {
        if (!resetArmed)
        {
            resetArmed = true;
            resetSave.Text = "Tap again to reset";
            return;
        }
        ResetSaveConfirmed?.Invoke();
    }

    public bool CloseOverlay()
    {
        if (consent.IsOpen)
        {
            consent.Close();
            return true;
        }
        return settings.Back();
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
        topBar.OffsetTop = top;
        topBar.OffsetBottom = top + topBar.GetCombinedMinimumSize().Y;
        bottomBar.OffsetBottom = -bottom;
        bottomBar.OffsetTop = -bottom - bottomBar.GetCombinedMinimumSize().Y;
    }
}
