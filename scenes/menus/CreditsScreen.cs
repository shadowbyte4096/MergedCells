using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using Godot;
using MergedCells.Core;

namespace MergedCells;

// The people who made the game, and behind a button every licence notice the builds need. Each licence paragraph is its
// own label, so Godot culls the ones off screen; one label holding all ~200 KB would draw every glyph every frame.
public partial class CreditsScreen : CanvasLayer, IScreen
{
    private const string CreditsPath = "res://credits/credits.json";

    // Labels made per frame, in milliseconds: building all ~500 paragraphs at once stalled a frame for 400 ms on desktop.
    // A drawing detail like BoardView.BuildBudgetMs, not config.
    private const double BuildBudgetMs = 4;

    private readonly Queue<(string Text, string Variation)> pending = new();

    private Control root;
    private Control layout;
    private ScrollContainer scroll;
    private Label title;
    private VBoxContainer people;
    private Control bottomBar;
    private VBoxContainer licences;
    private bool dark;
    private bool licencesQueued;
    private int built;
    private int buildFrames;
    private double worstFrame;

    public event Action BackPressed;

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
        scroll = GetNode<ScrollContainer>("Root/Layout/Scroll");
        title = GetNode<Label>("Root/Layout/TopBar/Row/TitleLabel");
        people = GetNode<VBoxContainer>("Root/Layout/Scroll/Margin/Column/People");
        bottomBar = GetNode<Control>("Root/Layout/BottomBar");
        licences = GetNode<VBoxContainer>("Root/Layout/Scroll/Margin/Column/Licences");
        GetNode<Button>("Root/Layout/TopBar/Row/Back").Pressed += () =>
        {
            if (!CloseOverlay())
            {
                BackPressed?.Invoke();
            }
        };
        GetNode<Button>("Root/Layout/BottomBar/ShowLicences").Pressed += ShowLicences;
        GetViewport().SizeChanged += ApplySafeArea;
        ApplyLook();
        ApplySafeArea();
    }

    public void Open()
    {
        AddPeople();
        GD.Print("Screen: credits");
    }

    // The licences count as a sheet over the people, so back returns to the people before it leaves the screen
    public bool CloseOverlay()
    {
        if (!licences.Visible)
        {
            return false;
        }
        ShowPage(licencesShown: false);
        GD.Print("Screen: credits");
        return true;
    }

    public override void _Process(double delta)
    {
        if (pending.Count == 0 || !licences.Visible)
        {
            return;
        }
        buildFrames++;
        worstFrame = Math.Max(worstFrame, delta);
        BuildSome();
        if (pending.Count == 0)
        {
            GD.Print($"Credits: built {built} paragraphs over {buildFrames + 1} frames, worst frame {worstFrame * 1000:0.0} ms");
        }
    }

    // The runtime a build ships is its target framework's (net8.0 on desktop, net9.0 on Android). Editor runs use
    // whatever .NET is installed, so the running version would pick the wrong notice there.
    private static int ShippedDotnetMajor() =>
        new FrameworkName(typeof(CreditsScreen).Assembly.GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName).Version.Major;

    private void ShowLicences()
    {
        ShowPage(licencesShown: true);
        GD.Print("Screen: licences");
        if (licencesQueued)
        {
            return;
        }
        licencesQueued = true;
        QueueBlock("Merged Cells", ReadNotice("res://credits/licences/merged-cells.txt"));
        QueueBlock("Godot Engine", Engine.GetLicenseText());
        QueueGodotComponents();
        QueueBlock("Inter", ReadNotice("res://themes/fonts/OFL.txt"));
        QueueBlock(".NET runtime", ReadNotice($"res://credits/licences/dotnet-{ShippedDotnetMajor()}.txt"));
        if (OS.HasFeature("android"))
        {
            QueueBlock("Android libraries", ReadNotice("res://credits/licences/android.txt"));
        }
        BuildSome();
    }

    private void ShowPage(bool licencesShown)
    {
        people.Visible = !licencesShown;
        bottomBar.Visible = !licencesShown;
        licences.Visible = licencesShown;
        title.Text = licencesShown ? "Licences" : "Credits";
        scroll.ScrollVertical = 0;
    }

    private void BuildSome()
    {
        var watch = Stopwatch.StartNew();
        while (pending.Count > 0 && watch.Elapsed.TotalMilliseconds < BuildBudgetMs)
        {
            var (text, variation) = pending.Dequeue();
            licences.AddChild(MakeLabel(text, variation, centred: false));
            built++;
        }
    }

    private static Label MakeLabel(string text, string variation, bool centred) => new()
    {
        Text = text,
        ThemeTypeVariation = variation,
        AutowrapMode = TextServer.AutowrapMode.WordSmart,
        HorizontalAlignment = centred ? HorizontalAlignment.Center : HorizontalAlignment.Left,
        SizeFlagsHorizontal = Control.SizeFlags.Fill,
    };

    private void AddPeople()
    {
        var text = FileAccess.GetFileAsString(CreditsPath);
        var openError = FileAccess.GetOpenError();
        if (openError != Error.Ok)
        {
            GD.PushError($"Credits: can't read {CreditsPath} ({openError})");
            return;
        }
        try
        {
            foreach (var entry in Credits.Parse(text))
            {
                people.AddChild(MakeLabel(entry.Role, "Muted", centred: true));
                foreach (var name in entry.Names)
                {
                    people.AddChild(MakeLabel(name, "CreditName", centred: true));
                }
            }
        }
        catch (System.Text.Json.JsonException exception)
        {
            GD.PushError($"Credits: {CreditsPath}: {exception.Message}");
        }
    }

    private void QueueGodotComponents()
    {
        Queue("Godot's third-party components", "LicenceHeading");
        foreach (var component in Engine.GetCopyrightInfo())
        {
            var parts = component["parts"].AsGodotArray()
                .Select(part => part.AsGodotDictionary())
                .Select(part => new ComponentPart(part["copyright"].AsStringArray(), part["license"].AsString()))
                .ToArray();
            Queue(NoticeText.Component(component["name"].AsString(), parts), "LicenceText");
        }
        foreach (var (name, text) in Engine.GetLicenseInfo())
        {
            QueueBlock(name.AsString(), text.AsString());
        }
    }

    private void QueueBlock(string heading, string text)
    {
        Queue(heading, "LicenceHeading");
        foreach (var paragraph in NoticeText.Paragraphs(text))
        {
            Queue(paragraph, "LicenceText");
        }
    }

    private void Queue(string text, string variation) => pending.Enqueue((text, variation));

    // A notice that can't be read says so on screen rather than stopping the game
    private static string ReadNotice(string path)
    {
        var text = FileAccess.GetFileAsString(path);
        var openError = FileAccess.GetOpenError();
        if (openError == Error.Ok)
        {
            return text;
        }
        GD.PushError($"Credits: can't read {path} ({openError})");
        return "This notice is missing from this build.";
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
