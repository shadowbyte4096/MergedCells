using System;
using Godot;
using MergedCells.Core;

namespace MergedCells;

// The app's root for the whole run: shows one screen at a time as its child, and owns the back gesture, the appearance
// and the save wiring
public partial class Main : Node
{
    // The frame-time log's window on test boards: a test detail, not a tunable number
    private const double FrameLogSeconds = 5;

    private Campaign campaign;
    private PlayRules campaignRules;
    private Node screen;
    private Action back;
    private bool atRoot;
    private Action pendingSwap;
    private bool dark;
    private bool? appliedDark;
    private readonly BackPresses backPresses = new();

    private Level level;
    private int? testBoard;
    private bool refreshLogged;
    private double frameWindow;
    private double frameWorst;
    private int frameCount;
    private int framesToSkip;

    [Export] public PackedScene ModeSelectScene { get; set; }

    [Export] public PackedScene LevelSelectScene { get; set; }

    [Export] public PackedScene CreditsScene { get; set; }

    [Export] public PackedScene ChapterCardScene { get; set; }

    [Export] public PackedScene PlayScene { get; set; }

    public override void _Notification(int what)
    {
        if (what == NotificationApplicationFocusIn)
        {
            ApplyAppearance();
        }
        else if (what == NotificationApplicationFocusOut || what == NotificationApplicationPaused)
        {
            // The back key's release may never arrive
            backPresses.Reset();
        }
        else if (what == NotificationWMGoBackRequest && backPresses.Request(Engine.GetProcessFrames()))
        {
            Back(fromSystem: true);
        }
    }

    public override void _Ready()
    {
        GD.Print("Merged Cells started (C#).");
        // The config first, so a bad file stops the game before anything appears
        _ = GameConfigFile.Current;
        _ = SaveGameFile.Current;
        ApplyAppearance();
        // Not every platform reports theme changes, so focus changes re-check too
        DisplayServer.SetSystemThemeChangeCallback(Callable.From(ApplyAppearance));

        campaign = CampaignFiles.Load();
        GD.Print($"Campaign: {Plural(campaign.Chapters.Count, "chapter")}, {Plural(campaign.Levels.Count, "level")}");
        try
        {
            campaignRules = new PlayRules(GameConfigFile.Current.Campaign.UndoLimit);
            _ = Progress();
        }
        catch (ArgumentOutOfRangeException exception)
        {
            GD.PushError($"res://config/game_config.json: {exception.Message}");
            GetTree().Quit(1);
            throw;
        }
        ShowModeSelect();
    }

    // On test boards only, for measuring drawing cost on a phone through logcat
    public override void _Process(double delta)
    {
        if (testBoard is null)
        {
            return;
        }
        // The frames that build, colour and first draw a new board aren't what's being measured
        if (framesToSkip > 0)
        {
            framesToSkip--;
            return;
        }
        frameWindow += delta;
        frameWorst = Math.Max(frameWorst, delta);
        frameCount++;
        if (frameWindow >= FrameLogSeconds)
        {
            GD.Print($"Frames: average {frameWindow / frameCount * 1000:0.0} ms, worst {frameWorst * 1000:0.0} ms over the last {FrameLogSeconds:0} s");
            ResetFrameLog();
        }
    }

    // Android's back key, read only to pair Godot's go-back requests with presses; the requests drive Back
    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventKey { Keycode: Godot.Key.Back } key)
        {
            return;
        }
        if (!key.Pressed)
        {
            backPresses.KeyUp(Engine.GetProcessFrames());
        }
        else if (!key.Echo)
        {
            backPresses.KeyDown(Engine.GetProcessFrames());
        }
    }

    // Esc on desktop. The settings sheet, deeper in the tree, sees it first and closes itself
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel"))
        {
            GetViewport().SetInputAsHandled();
            Back(fromSystem: false);
        }
    }

    public void ShowModeSelect() => Swap(() =>
    {
        var menu = Open<ModeSelect>(ModeSelectScene);
        menu.CampaignPressed += () => ShowLevelSelect(Progress().Furthest);
        menu.CreditsPressed += ShowCredits;
        menu.TestBoardsPressed += () => ShowTestBoard(0);
        menu.AppearanceChosen += OnAppearanceChosen;
        menu.Open(Progress().TotalStars, OS.IsDebugBuild(), SaveGameFile.Current.Appearance);
        back = null;
        atRoot = true;
    });

    public void ShowLevelSelect(Chapter chapter) => Swap(() =>
    {
        var select = Open<LevelSelect>(LevelSelectScene);
        select.LevelChosen += ShowLevel;
        select.BackPressed += ShowModeSelect;
        select.AppearanceChosen += OnAppearanceChosen;
        select.Open(campaign, Progress(), chapter, SaveGameFile.Current.Appearance);
        back = ShowModeSelect;
    });

    public void ShowCredits() => Swap(() =>
    {
        var credits = Open<CreditsScreen>(CreditsScene);
        credits.BackPressed += ShowModeSelect;
        credits.Open();
        back = ShowModeSelect;
    });

    public void ShowChapterCard(Chapter finished, Chapter next) => Swap(() =>
    {
        var card = Open<ChapterCard>(ChapterCardScene);
        card.StartPressed += () => ShowLevel(next.Levels[0]);
        card.AllChaptersPressed += () => ShowLevelSelect(next);
        card.Open(finished, next);
        back = () => ShowLevelSelect(next);
    });

    public void ShowLevel(Level next)
    {
        if (screen is Play current && pendingSwap is null)
        {
            PlayLevel(current, next);
            return;
        }
        Swap(() => PlayLevel(OpenPlay(), next));
    }

    public void ShowTestBoard(int index)
    {
        if (screen is Play current && pendingSwap is null)
        {
            PlayTestBoard(current, index);
            return;
        }
        Swap(() => PlayTestBoard(OpenPlay(), index));
    }

    public void OnAppearanceChosen(Appearance choice)
    {
        SaveGameFile.Current.Appearance = choice;
        SaveGameFile.Save();
        GD.Print($"Setting: appearance {choice.ToString().ToLowerInvariant()}");
        ApplyAppearance();
    }

    private CampaignProgress Progress() => CampaignProgress.Of(campaign, SaveGameFile.Current, GameConfigFile.Current);

    // Deferred, because a screen asks for the next one from inside its own button handlers. A request made while one is
    // pending replaces it, so a double tap opens one screen.
    private void Swap(Action open)
    {
        var queued = pendingSwap is not null;
        pendingSwap = open;
        if (!queued)
        {
            Callable.From(RunSwap).CallDeferred();
        }
    }

    private void RunSwap()
    {
        var open = pendingSwap;
        pendingSwap = null;
        open?.Invoke();
    }

    // Dark is set after the screen is in the tree, because screens find their nodes in _Ready
    private T Open<T>(PackedScene scene) where T : Node, IScreen
    {
        if (screen is not null)
        {
            RemoveChild(screen);
            screen.QueueFree();
        }
        var node = scene.Instantiate<T>();
        AddChild(node);
        node.Dark = dark;
        screen = node;
        atRoot = false;
        level = null;
        testBoard = null;
        return node;
    }

    private Play OpenPlay()
    {
        var play = Open<Play>(PlayScene);
        play.Appearance = SaveGameFile.Current.Appearance;
        play.AppearanceChosen += OnAppearanceChosen;
        play.NextPressed += OnNextPressed;
        play.MenuPressed += () => back?.Invoke();
        return play;
    }

    private void PlayLevel(Play play, Level next)
    {
        level = next;
        testBoard = null;
        var chapter = campaign.ChapterOf(next);
        var number = campaign.NumberInChapter(next);
        var stars = GameConfigFile.Current.StarsFor(next);
        var played = Progress().Stars(next) is { } earned
            ? $"best {SaveGameFile.Current.BestMoves(next)}, {Plural(earned, "star")}"
            : "not played";
        GD.Print($"Level {chapter.Number}-{number}: {next.Name} (id {next.Id}, par {next.Par}, {played})");
        play.Start(
            next.Map, next.Par, $"{number} · {next.Name}", stars, () => Progress().Next(next) is not null,
            moves => RecordFinish(next, moves, stars), campaignRules, next.Tutorial);
        back = () => ShowLevelSelect(chapter);
    }

    private void PlayTestBoard(Play play, int index)
    {
        level = null;
        testBoard = index;
        ResetFrameLog();
        framesToSkip = 2;
        var (title, build) = TestBoards.All[index];
        var map = build();
        var par = Solver.Solve(map).Par ?? throw new InvalidOperationException($"The test board \"{title}\" can't be solved.");
        if (!refreshLogged)
        {
            refreshLogged = true;
            GD.Print($"Screen refresh: {DisplayServer.ScreenGetRefreshRate():0} Hz");
        }
        GD.Print($"Level {title} (test board, par {par})");
        play.Start(map, par, title, GameConfigFile.Current.Stars, () => index + 1 < TestBoards.All.Count);
        back = ShowModeSelect;
    }

    private void OnNextPressed()
    {
        if (testBoard is { } index)
        {
            if (index + 1 < TestBoards.All.Count)
            {
                ShowTestBoard(index + 1);
            }
        }
        else if (level is not null && Progress().Next(level) is { } next)
        {
            var finished = campaign.ChapterOf(level);
            var following = campaign.ChapterOf(next);
            if (following.Number != finished.Number)
            {
                ShowChapterCard(finished, following);
            }
            else
            {
                ShowLevel(next);
            }
        }
    }

    // Asks the save at each finish, so a Retry compares with the best saved a moment ago
    private LevelFinish RecordFinish(Level finished, int moves, StarsConfig stars)
    {
        var openBefore = Progress().OpenChapters.Count;
        var finish = SaveGameFile.Current.RecordFinish(finished, moves, stars);
        if (finish.IsNewBest)
        {
            SaveGameFile.Save();
            var was = finish.PreviousBest is { } previous ? $"was {previous}" : "first finish";
            GD.Print($"New best: {Plural(moves, "move")} ({was})");
            var after = Progress();
            if (after.OpenChapters.Count > openBefore)
            {
                GD.Print($"Chapter {after.Furthest.Number} opened ({Plural(after.TotalStars, "star")})");
            }
        }
        return finish;
    }

    // An open sheet closes first. Otherwise the screen's way back; on the mode select Android's back quits and Esc does nothing
    private void Back(bool fromSystem)
    {
        GD.Print("Back");
        if (screen is IScreen current && current.CloseOverlay())
        {
            return;
        }
        if (atRoot)
        {
            if (fromSystem)
            {
                GetTree().Quit();
            }
            return;
        }
        back?.Invoke();
    }

    private void ApplyAppearance()
    {
        // The saved choice wins over the device unless it's System
        var wanted = SaveGameFile.Current.Appearance switch
        {
            Appearance.Light => false,
            Appearance.Dark => true,
            _ => DisplayServer.IsDarkModeSupported() && DisplayServer.IsDarkMode(),
        };
        if (appliedDark == wanted)
        {
            return;
        }
        appliedDark = wanted;
        dark = wanted;
        if (screen is IScreen current)
        {
            current.Dark = wanted;
        }
        GD.Print($"Appearance: {(dark ? "dark" : "light")}");
    }

    private void ResetFrameLog()
    {
        frameWindow = 0;
        frameWorst = 0;
        frameCount = 0;
    }

    private static string Plural(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";
}
