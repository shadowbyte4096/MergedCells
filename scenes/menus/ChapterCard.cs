using System;
using Godot;
using MergedCells.Core;

namespace MergedCells;

// Between two chapters, when Next carries the player out of a chapter's last level: the chapter just finished, the new
// one's number and name, its intro lines from the chapter file, and Start
public partial class ChapterCard : CanvasLayer, IScreen
{
    private Control root;
    private Control topBar;
    private bool dark;

    public event Action StartPressed;

    // The top bar's back chevron and "All chapters" both lead to the new chapter's page
    public event Action AllChaptersPressed;

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
        topBar = GetNode<Control>("Root/TopBar");
        GetNode<Button>("Root/Centre/Column/Start").Pressed += () => StartPressed?.Invoke();
        GetNode<Button>("Root/Centre/Column/AllChapters").Pressed += () => AllChaptersPressed?.Invoke();
        GetNode<Button>("Root/TopBar/Row/Back").Pressed += () => AllChaptersPressed?.Invoke();
        GetViewport().SizeChanged += ApplySafeArea;
        ApplyLook();
        ApplySafeArea();
    }

    public void Open(Chapter finished, Chapter next)
    {
        ArgumentNullException.ThrowIfNull(finished);
        ArgumentNullException.ThrowIfNull(next);
        GetNode<Label>("Root/Centre/Column/Completed").Text = $"Chapter {finished.Number} complete";
        GetNode<Label>("Root/Centre/Column/Number").Text = $"Chapter {next.Number}";
        GetNode<Label>("Root/Centre/Column/ChapterName").Text = next.Name;
        GetNode<Button>("Root/Centre/Column/Start").Text = $"Start chapter {next.Number}";
        var intro = GetNode<VBoxContainer>("Root/Centre/Column/Intro");
        foreach (var line in next.Intro)
        {
            intro.AddChild(new Label
            {
                Text = line,
                ThemeTypeVariation = "TutorialDetail",
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            });
        }
        GD.Print($"Screen: chapter card, {finished.Number} complete, {next.Number} ({next.Name}) next");
    }

    public bool CloseOverlay() => false;

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
