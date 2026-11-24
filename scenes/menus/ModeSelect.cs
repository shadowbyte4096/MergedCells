using System;
using Godot;
using MergedCells.Core;

namespace MergedCells;

// The first screen: the title, the star total and coin balance, Campaign, Custom levels, Infinite, Credits and the gear
public partial class ModeSelect : CanvasLayer, IScreen
{
    private Control root;
    private Control topBar;
    private Control bottomBar;
    private StarsView star;
    private Label totalLabel;
    private Label coinsLabel;
    private Label saveNotice;
    private Label title;
    private SettingsSheet settings;
    private ConsentSheet consent;
    private bool dark;

    public event Action CampaignPressed;

    public event Action CustomLevelsPressed;

    public event Action InfinitePressed;

    public event Action CreditsPressed;

    [Export] public AppLooks Looks { get; set; }

    public SettingsSheet Settings => settings;

    // The one-time ask about play data and, from chunk 33, leaderboards; Main decides what it asks
    public ConsentSheet Consent => consent;

    // Where the title sits, for the intro's title to land on
    public TitleSpot TitleSpot
    {
        get
        {
            var rect = title.GetGlobalRect();
            var centre = rect.GetCenter();
            return new TitleSpot(new System.Numerics.Vector2(centre.X, centre.Y), rect.Size.X, title.GetThemeFontSize("font_size"));
        }
    }

    public Color TitleColour => title.GetThemeColor("font_color");

    // Hidden while the intro's own title glides onto it, so only one title ever shows; transparent rather than invisible,
    // so the column keeps its layout
    public bool TitleShown
    {
        set => title.SelfModulate = value ? Colors.White : Colors.Transparent;
    }

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
        coinsLabel = GetNode<Label>("Root/Centre/Column/Coins/CoinsLabel");
        saveNotice = GetNode<Label>("Root/Centre/Column/SaveNotice");
        title = GetNode<Label>("Root/Centre/Column/Title");
        settings = GetNode<SettingsSheet>("Root/Settings");
        consent = GetNode<ConsentSheet>("Root/Consent");

        star.Looks = [StarLook.Earned];
        GetNode<Button>("Root/Centre/Column/Campaign").Pressed += () => CampaignPressed?.Invoke();
        GetNode<Button>("Root/Centre/Column/CustomLevels").Pressed += () => CustomLevelsPressed?.Invoke();
        GetNode<Button>("Root/BottomBar/Credits").Pressed += () => CreditsPressed?.Invoke();
        GetNode<Button>("Root/Centre/Column/Infinite").Pressed += () => InfinitePressed?.Invoke();
        GetNode<Button>("Root/TopBar/Row/SettingsButton").Pressed += () => settings.Open();
        GetViewport().SizeChanged += ApplySafeArea;
        ApplyLook();
        ApplySafeArea();
    }

    // saveNotice: why this run's progress isn't being kept, or null while saving works
    public void Open(int totalStars, long coins, string saveNotice)
    {
        totalLabel.Text = totalStars.ToString();
        coinsLabel.Text = CoinWords.Balance(coins);
        ShowSaveNotice(saveNotice);
        GD.Print("Screen: mode select");
    }

    // Also after an answer to the ask that the save couldn't keep
    public void ShowSaveNotice(string notice)
    {
        saveNotice.Text = notice ?? "";
        saveNotice.Visible = notice != null;
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
