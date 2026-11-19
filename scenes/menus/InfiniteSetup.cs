using System;
using Godot;
using MergedCells.Core;

namespace MergedCells;

// Infinite mode's setup: difficulty and map size on sliders, the twist switches, and Play. Every change goes through
// InfiniteChoice, which keeps the size inside what the difficulty allows for the switches as they are. While a map is
// being made, Play becomes its loading bar
public partial class InfiniteSetup : CanvasLayer, IScreen, IMapWait
{
    private Control root;
    private Control topBar;
    private Button play;
    private Button gear;
    private MakingBar bar;
    private Button cancel;
    // Dimmed while the bar shows, and taking no touches or keys from the press on
    private Control[] controls;
    private bool waiting;
    private bool showing;
    private HSlider difficultySlider;
    private Button difficultyLess;
    private Button difficultyMore;
    private Label difficultyValue;
    private HSlider sizeSlider;
    private Button sizeLess;
    private Button sizeMore;
    private Label sizeValue;
    private SliderHatch hatch;
    private Label rangeLine;
    private Button walls;
    private Button traps;
    private Button teleporters;
    private Button irregular;
    private Label notice;
    private SettingsSheet settings;
    private InfiniteConfig config;
    private InfiniteChoice choice;
    private bool dark;

    public event Action<InfiniteChoice> PlayPressed;

    public event Action BackPressed;

    public event Action WaitCancelled;

    [Export] public AppLooks Looks { get; set; }

    public SettingsSheet Settings => settings;

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
        const string column = "Root/Centre/Column";
        difficultySlider = GetNode<HSlider>($"{column}/DifficultyRow/Slider");
        difficultyLess = GetNode<Button>($"{column}/DifficultyRow/Less");
        difficultyMore = GetNode<Button>($"{column}/DifficultyRow/More");
        difficultyValue = GetNode<Label>($"{column}/DifficultyHeader/Value");
        sizeSlider = GetNode<HSlider>($"{column}/SizeRow/Slider");
        sizeLess = GetNode<Button>($"{column}/SizeRow/Less");
        sizeMore = GetNode<Button>($"{column}/SizeRow/More");
        sizeValue = GetNode<Label>($"{column}/SizeHeader/Value");
        hatch = GetNode<SliderHatch>($"{column}/SizeRow/Slider/Hatch");
        rangeLine = GetNode<Label>($"{column}/RangeLine");
        walls = GetNode<Button>($"{column}/Twists/Walls");
        traps = GetNode<Button>($"{column}/Twists/Traps");
        teleporters = GetNode<Button>($"{column}/Twists/Teleporters");
        irregular = GetNode<Button>($"{column}/Twists/Irregular");
        notice = GetNode<Label>($"{column}/Notice");
        settings = GetNode<SettingsSheet>("Root/Settings");

        difficultySlider.ValueChanged += value => Choose(choice.WithDifficulty((int)value, config));
        difficultyLess.Pressed += () => Choose(choice.WithDifficulty(choice.Difficulty - 1, config));
        difficultyMore.Pressed += () => Choose(choice.WithDifficulty(choice.Difficulty + 1, config));
        sizeSlider.ValueChanged += value => Choose(choice.WithSide((int)value, config));
        sizeLess.Pressed += () => Choose(choice.WithSide(choice.Side - 1, config));
        sizeMore.Pressed += () => Choose(choice.WithSide(choice.Side + 1, config));
        walls.Toggled += on => Choose(choice.WithWalls(on, config));
        traps.Toggled += on => Choose(choice.WithTraps(on, config));
        teleporters.Toggled += on => Choose(choice.WithTeleporters(on, config));
        irregular.Toggled += on => Choose(choice.WithIrregular(on));
        play = GetNode<Button>($"{column}/Play");
        play.Pressed += Play;
        BuildWait();
        controls =
        [
            GetNode<Control>($"{column}/DifficultyHeader"),
            GetNode<Control>($"{column}/DifficultyRow"),
            GetNode<Control>($"{column}/SizeHeader"),
            GetNode<Control>($"{column}/SizeRow"),
            rangeLine,
            GetNode<Control>($"{column}/TwistsLabel"),
            GetNode<Control>($"{column}/Twists"),
            notice,
        ];
        GetNode<Button>("Root/TopBar/Row/Back").Pressed += () => BackPressed?.Invoke();
        gear = GetNode<Button>("Root/TopBar/Row/SettingsButton");
        gear.Pressed += () =>
        {
            if (!waiting)
            {
                settings.Open();
            }
        };
        GetViewport().SizeChanged += ApplySafeArea;
        ApplyLook();
        ApplySafeArea();
    }

    // notice: why the last map couldn't be made, or null
    public void Open(InfiniteChoice opened, InfiniteConfig infinite, string notice)
    {
        // Set first: changing a slider's range can move its value, which reports a change
        config = infinite;
        choice = opened.Fitted(infinite);
        difficultySlider.MinValue = 1;
        difficultySlider.MaxValue = InfiniteConfig.DifficultyCount;
        sizeSlider.MinValue = infinite.MinSide;
        sizeSlider.MaxValue = infinite.MaxSide;
        this.notice.Text = notice ?? "";
        this.notice.Visible = notice is not null;
        Show(choice);
        GD.Print("Screen: infinite setup");
    }

    public bool CloseOverlay() => settings.Back();

    // Enter plays on desktop
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Echo: false } && @event.IsActionPressed("ui_accept") && !settings.IsOpen)
        {
            GetViewport().SetInputAsHandled();
            Play();
        }
    }

    private void Play()
    {
        if (choice is not null && !waiting)
        {
            PlayPressed?.Invoke(choice);
        }
    }

    public void BeginWait()
    {
        waiting = true;
        foreach (var control in controls)
        {
            control.MouseBehaviorRecursive = Control.MouseBehaviorRecursiveEnum.Disabled;
            control.FocusBehaviorRecursive = Control.FocusBehaviorRecursiveEnum.Disabled;
        }
    }

    public void ShowWait(double progress)
    {
        if (!showing)
        {
            showing = true;
            // Self-modulate hides the pill and its text but not the bar and Cancel, its children, so nothing moves
            play.SelfModulate = Colors.Transparent;
            bar.Visible = true;
            cancel.Visible = true;
            var dimmed = new Color(1, 1, 1, root.GetThemeConstant("dim_percent", "Waiting") / 100f);
            foreach (var control in controls)
            {
                control.Modulate = dimmed;
            }
        }
        bar.Progress = progress;
    }

    public void EndWait()
    {
        waiting = false;
        showing = false;
        play.SelfModulate = Colors.White;
        bar.Visible = false;
        bar.Progress = 0;
        cancel.Visible = false;
        foreach (var control in controls)
        {
            control.Modulate = Colors.White;
            control.MouseBehaviorRecursive = Control.MouseBehaviorRecursiveEnum.Inherited;
            control.FocusBehaviorRecursive = Control.FocusBehaviorRecursiveEnum.Inherited;
        }
    }

    // The bar fills Play's place and Cancel hangs below it, both outside the column's layout, so nothing moves
    private void BuildWait()
    {
        bar = new MakingBar { Visible = false, Text = "Making map…" };
        play.AddChild(bar);
        bar.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        cancel = new Button { Visible = false, Text = "Cancel", ThemeTypeVariation = "CaptionButton", FocusMode = Control.FocusModeEnum.None };
        play.AddChild(cancel);
        cancel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide);
        cancel.OffsetTop = 0;
        cancel.OffsetBottom = 0;
        cancel.GrowVertical = Control.GrowDirection.End;
        cancel.Pressed += () => WaitCancelled?.Invoke();
    }

    private void Choose(InfiniteChoice chosen)
    {
        if (waiting)
        {
            // A key on a slider that had focus before the press; the choice stays as it was
            Show(choice);
            return;
        }
        if (chosen != choice)
        {
            Show(chosen);
        }
        else
        {
            // A drag into the hatched part puts the knob back at the edge of what's allowed
            sizeSlider.SetValueNoSignal(choice.Side);
        }
    }

    private void Show(InfiniteChoice shown)
    {
        choice = shown;
        difficultySlider.SetValueNoSignal(shown.Difficulty);
        sizeSlider.SetValueNoSignal(shown.Side);
        difficultyValue.Text = shown.Difficulty.ToString();
        sizeValue.Text = $"{shown.Side} × {shown.Side}";
        difficultyLess.Disabled = shown.Difficulty <= 1;
        difficultyMore.Disabled = shown.Difficulty >= InfiniteConfig.DifficultyCount;
        var sizes = shown.Sizes(config);
        sizeLess.Disabled = shown.Side <= sizes.MinSide;
        sizeMore.Disabled = shown.Side >= sizes.MaxSide;
        var span = (float)(config.MaxSide - config.MinSide);
        hatch.SetBlocked(span > 0 ? (sizes.MinSide - config.MinSide) / span : 0, span > 0 ? (sizes.MaxSide - config.MinSide) / span : 1);
        var narrower = sizes.MinSide > config.MinSide || sizes.MaxSide < config.MaxSide;
        rangeLine.Text = narrower ? $"At difficulty {shown.Difficulty}: {sizes.MinSide} to {sizes.MaxSide}" : "";
        // The line keeps its space when empty, so the column doesn't jump as the range changes
        walls.SetPressedNoSignal(shown.Walls);
        traps.SetPressedNoSignal(shown.Traps);
        teleporters.SetPressedNoSignal(shown.Teleporters);
        irregular.SetPressedNoSignal(shown.Irregular);
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
        var (top, _) = SafeArea.Insets(this);
        topBar.OffsetTop = top;
        topBar.OffsetBottom = top + topBar.GetCombinedMinimumSize().Y;
    }
}
