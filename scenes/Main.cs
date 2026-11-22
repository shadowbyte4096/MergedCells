using System;
using System.Linq;
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
    // The editor's tool, kept while the player is in the editor or its "Play it" (the session is kept by the screens' handlers)
    private EditorTool editorTool;
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

    [Export] public PackedScene EditorScene { get; set; }

    [Export] public PackedScene CustomLevelsScene { get; set; }

    [Export] public PackedScene LibraryScene { get; set; }

    [Export] public PackedScene InfiniteSetupScene { get; set; }

    public override void _Notification(int what)
    {
        if (what == NotificationApplicationFocusIn)
        {
            ApplyAppearance();
            // Back from the background, or online again: anything waiting goes
            WakeServer();
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
        else if (what == NotificationWMCloseRequest)
        {
            CancelMapJob();
        }
    }

    public override void _ExitTree() => CancelMapJob();

    public override void _Ready()
    {
        GD.Print("Merged Cells started (C#).");
        GD.Print($"Build: {BuildName.Current}");
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
            GameConfigFile.Current.CheckRanges(campaign.Levels);
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
        StartServer();
    }

    // The background map first; then, on test boards only, the frame log for measuring drawing cost on a phone through logcat
    public override void _Process(double delta)
    {
        PollMapJob();
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
        menu.CustomLevelsPressed += ShowCustomLevels;
        menu.CreditsPressed += ShowCredits;
        menu.InfinitePressed += () => ShowInfiniteSetup();
        menu.Open(Progress().TotalStars, SaveGameFile.Notice);
        WireConsent(menu);
        back = null;
        atRoot = true;
    });

    public void ShowLevelSelect(Chapter chapter) => Swap(() =>
    {
        var select = Open<LevelSelect>(LevelSelectScene);
        select.LevelChosen += ShowLevel;
        select.BackPressed += ShowModeSelect;
        select.Open(campaign, Progress(), chapter);
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

    public void ShowCustomLevels() => ShowCustomLevels(null);

    // shown: an import just made, whose sheet opens over the reloaded list
    private void ShowCustomLevels(ImportResult shown) => Swap(() =>
    {
        var list = Open<CustomLevelsScreen>(CustomLevelsScene);
        list.BackPressed += ShowModeSelect;
        list.NewPressed += NewCustomLevel;
        list.ImportPressed += ImportCustomLevel;
        WireLibrary(list);
        list.EditPressed += custom =>
        {
            if (custom.IsImported)
            {
                OpenCopy(CustomLevelCopies.OwnCopy(custom, Guid.NewGuid(), Now()));
            }
            else
            {
                ShowEditor(NewSession(custom));
            }
        };
        list.EditCopyPressed += custom => OpenCopy(CustomLevelCopies.OwnCopy(custom, Guid.NewGuid(), Now()));
        list.CopyChosen += level =>
            OpenCopy(CustomLevelCopies.FromCampaign(level, Guid.NewGuid(), Now(), GameConfigFile.Current.CustomLevels.MaxNameLength));
        list.PlayPressed += custom => PlayCustom(custom, ShowCustomLevels, record: true);
        list.SharePressed += custom =>
        {
            var code = ShareCode.Write(custom);
            DisplayServer.ClipboardSet(code);
            // Only the player's own levels: an import of one of them stays theirs, so it can't be imported back for coins
            if (!custom.IsImported && SaveGameFile.Current.RecordShared(custom.Id))
            {
                SaveGameFile.Save();
            }
            GD.Print($"Custom level shared: {custom.Name} ({code.Length} characters)");
            list.ShowCopied();
        };
        list.DeletePressed += custom =>
        {
            CustomLevelFiles.Delete(custom.Id);
            GD.Print($"Custom level deleted: {custom.Name}");
            ShowCustomLevels();
        };
        var copyable = Progress().ThreeStarred
            .Select(level => new CopyChoice(campaign.ChapterOf(level), campaign.NumberInChapter(level), level))
            .ToArray();
        list.Open(
            CustomLevelFiles.LoadAll(),
            new DifficultyRater(GameConfigFile.Current.Difficulty),
            GameConfigFile.Current.CustomLevels,
            CustomLevelFiles.Notice,
            copyable,
            SaveGameFile.Current.Published,
            PublishStateOf,
            shown);
        back = ShowModeSelect;
    });

    public void ShowEditor(EditSession session) => Swap(() =>
    {
        var editor = Open<LevelEditor>(EditorScene);
        editor.Changed += custom =>
        {
            CustomLevelFiles.Write(custom);
            editor.SaveProblem = CustomLevelFiles.Notice;
        };
        editor.ToolChosen += tool => editorTool = tool;
        // "Play it" is the author testing an unfinished level, so it isn't recorded
        editor.PlayPressed += custom => PlayCustom(custom, () => ShowEditor(session), record: false);
        editor.BackPressed += LeaveEditor;
        editor.Open(session, editorTool);
        editor.SaveProblem = CustomLevelFiles.Notice;
        back = LeaveEditor;
    });

    // backTo: where the back chevron, Menu and Android's back go, the editor or the list
    public void PlayCustom(CustomLevel custom, Action backTo, bool record) => Swap(() =>
    {
        var play = OpenPlay();
        var par = Solver.Solve(custom.Map).Par
            ?? throw new InvalidOperationException($"The custom level \"{custom.Name}\" can't be won.");
        GD.Print($"Custom level: {custom.Name} (par {par})");
        play.Start(custom.Map, par, new PlaySetup(custom.Name)
        {
            Rules = new PlayRules(GameConfigFile.Current.CustomLevels.UndoLimit),
            Recording = record ? CustomRecording(custom, par) : null,
            Library = LibraryCardFor(custom),
        });
        back = backTo;
    });

    public void OnAppearanceChosen(Appearance choice)
    {
        SaveGameFile.Current.Appearance = choice;
        SaveGameFile.Save();
        GD.Print($"Setting: appearance {choice.ToString().ToLowerInvariant()}");
        ApplyAppearance();
    }

    private void LeaveEditor()
    {
        editorTool = EditorTool.Blocks;
        ShowCustomLevels();
    }

    private void NewCustomLevel(int rows, int columns)
    {
        var session = NewSession(new CustomLevel(Guid.NewGuid(), CustomLevelNames.Untitled, LevelEdits.Blank(rows, columns), Now()));
        CustomLevelFiles.Write(session.Level);
        GD.Print($"Custom level made: {columns}×{rows}");
        ShowEditor(session);
    }

    // A copy of an imported or campaign level: the player's own, written at once like a new level, and opened in the editor
    private void OpenCopy(CustomLevel copy)
    {
        CustomLevelFiles.Write(copy);
        GD.Print($"Custom level copied: {copy.Name}");
        ShowEditor(NewSession(copy));
    }

    // Reads the clipboard, writes the level when the import brings one, and reopens the list with what happened
    private void ImportCustomLevel()
    {
        var result = LevelImport.Of(
            DisplayServer.ClipboardGet(),
            CustomLevelFiles.LoadAll(),
            SaveGameFile.Current.SharedLevels,
            GameConfigFile.Current.CustomLevels,
            DateTimeOffset.UtcNow);
        var name = result.Level is { } level ? $" ({level.Name})" : "";
        if (result.Level is not null)
        {
            CustomLevelFiles.Write(result.Level);
            // The notice is set by a write that fails and cleared by one that works
            if (CustomLevelFiles.Notice is not null)
            {
                result = result.NotSaved();
            }
        }
        GD.Print($"Custom level import: {result.Outcome}{name}");
        ShowCustomLevels(result);
    }

    private static DateTimeOffset Now() => WholeSeconds.Of(DateTimeOffset.UtcNow);

    // Edits are stamped with the device's clock
    private static EditSession NewSession(CustomLevel custom) => new(custom, () => DateTimeOffset.UtcNow);

    private CampaignProgress Progress() => CampaignProgress.Of(campaign, SaveGameFile.Current, GameConfigFile.Current);

    // Deferred, because a screen asks for the next one from inside its own button handlers. A request made while one is
    // pending replaces it, so a double tap opens one screen.
    private void Swap(Action open)
    {
        CancelMapJob();
        var queued = pendingSwap is not null;
        pendingSwap = open;
        if (!queued)
        {
            Callable.From(RunSwap).CallDeferred();
        }
    }

    // A press in the same frame as the request can start a map on the screen that's leaving; this stops it with its screen
    private void RunSwap()
    {
        CancelMapJob();
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
        if (((IScreen)node).Settings is { } sheet)
        {
            WireSettings(sheet);
        }
        screen = node;
        atRoot = false;
        level = null;
        testBoard = null;
        infiniteChoice = null;
        return node;
    }

    private Play OpenPlay()
    {
        var play = Open<Play>(PlayScene);
        play.NextPressed += OnNextPressed;
        play.NewMapPressed += OnNewMapPressed;
        play.MenuPressed += () => back?.Invoke();
        play.WaitCancelled += () => CancelWait(play);
        return play;
    }

    private void PlayLevel(Play play, Level next)
    {
        level = next;
        testBoard = null;
        infiniteChoice = null;
        var chapter = campaign.ChapterOf(next);
        var number = campaign.NumberInChapter(next);
        var stars = GameConfigFile.Current.StarsFor(next);
        var played = Progress().Stars(next) is { } earned
            ? $"best {SaveGameFile.Current.BestMoves(next)}, {Plural(earned, "star")}"
            : "not played";
        GD.Print($"Level {chapter.Number}-{number}: {next.Name} (id {next.Id}, par {next.Par}, {played})");
        play.Start(next.Map, next.Par, new PlaySetup($"{number} · {next.Name}")
        {
            Stars = stars,
            HasNext = () => Progress().Next(next) is not null,
            RecordFinish = moves => RecordFinish(next, moves, stars),
            Rules = campaignRules,
            Tutorial = next.Tutorial,
            Recording = CampaignRecording(next),
            // Asked as the card appears, so a player who turned leaderboards off mid-level never sees the area
            Leaderboard = () => LeaderboardFor(next),
        });
        back = () => ShowLevelSelect(chapter);
    }

    private void PlayTestBoard(Play play, int index)
    {
        level = null;
        testBoard = index;
        infiniteChoice = null;
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
        play.Start(map, par, new PlaySetup(title) { Stars = GameConfigFile.Current.Stars, HasNext = () => index + 1 < TestBoards.All.Count });
        back = ShowModeSelect;
    }

    private void OnNextPressed()
    {
        if (infiniteChoice is { } choice)
        {
            PlayInfinite(choice);
        }
        else if (testBoard is { } index)
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
            WakeServer();
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
