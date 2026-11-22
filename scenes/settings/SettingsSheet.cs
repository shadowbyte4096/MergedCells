using System;
using Godot;
using MergedCells.Core;

namespace MergedCells;

// The settings card over a wash, in the results card's style, with a second page, "Privacy and data", swapped in place.
// Main wires every screen's sheet (IScreen.Settings): it supplies the values the sheet opens on and hears each choice. A
// choice applies at once and leaves the sheet open; Done or a tap on the wash closes it, and Back or Esc goes back a page.
public partial class SettingsSheet : Control
{
    private const string ArmedDelete = "Tap again to delete";
    private const string CopyIdText = "Copy player ID";

    private Control mainPage;
    private Control privacyPage;
    private Control card;
    private ScrollContainer privacyScroll;
    private Control privacyBody;
    private float topInset;
    private Button system;
    private Button light;
    private Button dark;
    private CheckButton playDataSwitch;
    private Label playDataNote;
    private CheckButton leaderboardsSwitch;
    private CheckButton librarySwitch;
    private Label cantChange;
    private string cantChangeText;
    private Button delete;
    private string deleteText;
    private Label deleteResult;
    private Label playerId;
    private Button copyId;
    private Guid? player;
    private Appearance current;
    private bool deleteArmed;

    public event Action<Appearance> AppearanceChosen;

    public event Action<ConsentWording, bool> ConsentChosen;

    // After the second press: the first only asks
    public event Action DeleteConfirmed;

    public event Action Closed;

    // Set by Main when the screen opens
    public Func<SheetValues> Current { get; set; }

    public bool IsOpen => Visible;

    public override void _Ready()
    {
        mainPage = GetNode<Control>("Sheet/Column");
        privacyPage = GetNode<Control>("Sheet/Privacy");
        card = GetNode<Control>("Sheet");
        privacyScroll = GetNode<ScrollContainer>("Sheet/Privacy/Scroll");
        privacyBody = GetNode<Control>("Sheet/Privacy/Scroll/Body");
        system = GetNode<Button>("Sheet/Column/Switch/System");
        light = GetNode<Button>("Sheet/Column/Switch/Light");
        dark = GetNode<Button>("Sheet/Column/Switch/Dark");
        playDataSwitch = GetNode<CheckButton>("Sheet/Privacy/Scroll/Body/PlayDataSwitch");
        playDataNote = GetNode<Label>("Sheet/Privacy/Scroll/Body/PlayDataNote");
        leaderboardsSwitch = GetNode<CheckButton>("Sheet/Privacy/Scroll/Body/LeaderboardsSwitch");
        var leaderboardsNote = GetNode<Label>("Sheet/Privacy/Scroll/Body/LeaderboardsNote");
        librarySwitch = GetNode<CheckButton>("Sheet/Privacy/Scroll/Body/LibrarySwitch");
        var libraryNote = GetNode<Label>("Sheet/Privacy/Scroll/Body/LibraryNote");
        var terms = GetNode<Button>("Sheet/Privacy/Scroll/Body/Links/Terms");
        cantChange = GetNode<Label>("Sheet/Privacy/Scroll/Body/CantChange");
        cantChangeText = cantChange.Text;
        delete = GetNode<Button>("Sheet/Privacy/Scroll/Body/Delete");
        deleteText = delete.Text;
        deleteResult = GetNode<Label>("Sheet/Privacy/Scroll/Body/DeleteResult");
        playerId = GetNode<Label>("Sheet/Privacy/Scroll/Body/PlayerId");
        copyId = GetNode<Button>("Sheet/Privacy/Scroll/Body/CopyId");

        // The texts that go with each kind's answer live with its wording version in Core
        playDataSwitch.Text = ConsentWordings.PlayData.SwitchLabel;
        leaderboardsSwitch.Text = ConsentWordings.Leaderboards.SwitchLabel;
        leaderboardsNote.Text = ConsentWordings.Leaderboards.SwitchNote;
        leaderboardsSwitch.Visible = ConsentWordings.Leaderboards.Offered;
        leaderboardsNote.Visible = ConsentWordings.Leaderboards.Offered;
        librarySwitch.Text = ConsentWordings.Library.SwitchLabel;
        libraryNote.Text = ConsentWordings.Library.SwitchNote;
        librarySwitch.Visible = ConsentWordings.Library.Offered;
        libraryNote.Visible = ConsentWordings.Library.Offered;
        // Turning the library on accepts its terms, so they sit on the page too
        terms.Visible = ConsentWordings.Library.Offered;

        system.Pressed += () => Choose(Appearance.System);
        light.Pressed += () => Choose(Appearance.Light);
        dark.Pressed += () => Choose(Appearance.Dark);
        GetNode<Button>("Sheet/Column/PrivacyRow").Pressed += () => ShowPage(privacy: true);
        GetNode<Button>("Sheet/Privacy/Header/Back").Pressed += () => ShowPage(privacy: false);
        GetNode<Button>("Sheet/Column/DiscordRow").Pressed += () => OS.ShellOpen(Community.DiscordUrl);
        playDataSwitch.Toggled += on => Choose(ConsentWordings.PlayData, on);
        leaderboardsSwitch.Toggled += on => Choose(ConsentWordings.Leaderboards, on);
        librarySwitch.Toggled += on => Choose(ConsentWordings.Library, on);
        delete.Pressed += OnDeletePressed;
        copyId.Pressed += OnCopyIdPressed;
        terms.Pressed += () =>
        {
            Disarm();
            OS.ShellOpen(ConsentWordings.TermsUrl);
        };
        GetNode<Button>("Sheet/Privacy/Scroll/Body/Links/Policy").Pressed += () =>
        {
            Disarm();
            OS.ShellOpen(ConsentWordings.PolicyUrl);
        };
        GetNode<Button>("Sheet/Column/Done").Pressed += Close;
        GetNode<Button>("Sheet/Privacy/Done").Pressed += Close;
        GetNode<Control>("Wash").GuiInput += OnWashInput;
    }

    // Always on the main page
    public void Open()
    {
        ShowPage(privacy: false);
        Refresh();
        Show();
    }

    // Reads the values again, as after a choice or a deletion
    public void Refresh()
    {
        var values = Current();
        current = values.Appearance;
        // Without the signal, so opening never counts as a choice
        system.SetPressedNoSignal(values.Appearance == Appearance.System);
        light.SetPressedNoSignal(values.Appearance == Appearance.Light);
        dark.SetPressedNoSignal(values.Appearance == Appearance.Dark);
        playDataSwitch.SetPressedNoSignal(values.PlayData);
        leaderboardsSwitch.SetPressedNoSignal(values.Leaderboards);
        librarySwitch.SetPressedNoSignal(values.Library);
        playDataSwitch.Disabled = !values.CanChange;
        leaderboardsSwitch.Disabled = !values.CanChange;
        librarySwitch.Disabled = !values.CanChange;
        var waiting = values.Waiting switch
        {
            0 => "",
            1 => " 1 map is waiting to be sent.",
            _ => $" {values.Waiting} maps are waiting to be sent.",
        };
        playDataNote.Text = ConsentWordings.PlayData.SwitchNote + waiting;
        // A save that can't be written at all, or the last write that failed: an answer or deletion then isn't kept
        cantChange.Text = values.CanChange ? values.SaveProblem : cantChangeText;
        cantChange.Visible = !values.CanChange || values.SaveProblem != null;
        delete.Visible = values.HasData;
        delete.Disabled = !values.CanChange;
        // Without accounts the ID is the only way to name a player's data in an email; "Delete my data" drops it
        player = values.Player;
        playerId.Text = player is { } id ? $"Your player ID: {id:D}" : "";
        playerId.Visible = player is not null;
        copyId.Visible = player is not null;
        copyId.Text = CopyIdText;
        Disarm();
    }

    public void ShowDeleteResult(string sentence)
    {
        deleteResult.Text = sentence;
        deleteResult.Show();
    }

    // The server's answer after the line was shown; nothing once the line has gone (the page left or the sheet closed)
    public void UpdateDeleteResult(string sentence)
    {
        if (deleteResult.Visible)
        {
            deleteResult.Text = sentence;
        }
    }

    // Back and Esc: the privacy page goes back to the main page, the main page closes. False when the sheet isn't open
    public bool Back()
    {
        if (!Visible)
        {
            return false;
        }
        if (privacyPage.Visible)
        {
            ShowPage(privacy: false);
        }
        else
        {
            Close();
        }
        return true;
    }

    // From either page
    public void Close()
    {
        if (!Visible)
        {
            return;
        }
        Disarm();
        Hide();
        Closed?.Invoke();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Visible && @event.IsActionPressed("ui_cancel"))
        {
            GetViewport().SetInputAsHandled();
            Back();
        }
    }

    private void ShowPage(bool privacy)
    {
        Disarm();
        deleteResult.Hide();
        copyId.Text = CopyIdText;
        mainPage.Visible = !privacy;
        privacyPage.Visible = privacy;
        if (privacy)
        {
            topInset = SafeArea.Insets(this).Top;
            privacyScroll.ScrollVertical = 0;
        }
    }

    // With three kinds of data the page can be taller than the screen, so its middle scrolls and the card stops a gap below
    // the top of the safe area. Worked out every frame the page shows, because the notes rewrap and the result lines come
    // and go
    public override void _Process(double delta)
    {
        if (!Visible || !privacyPage.Visible)
        {
            return;
        }
        const float TopGap = 24;
        var others = card.GetCombinedMinimumSize().Y - privacyScroll.CustomMinimumSize.Y;
        var available = GetViewportRect().Size.Y - topInset - TopGap - others;
        var height = Mathf.Max(0, Mathf.Min(privacyBody.GetCombinedMinimumSize().Y, available));
        if (!Mathf.IsEqualApprox(privacyScroll.CustomMinimumSize.Y, height))
        {
            privacyScroll.CustomMinimumSize = new Vector2(0, height);
        }
    }

    private void OnDeletePressed()
    {
        if (!deleteArmed)
        {
            deleteArmed = true;
            delete.Text = ArmedDelete;
            return;
        }
        Disarm();
        DeleteConfirmed?.Invoke();
    }

    private void OnCopyIdPressed()
    {
        Disarm();
        if (player is { } id)
        {
            DisplayServer.ClipboardSet(id.ToString("D"));
            copyId.Text = "Copied";
        }
    }

    private void Disarm()
    {
        deleteArmed = false;
        delete.Text = deleteText;
    }

    // On release rather than press: closing on the press would let the same touch's drag reach the board as a move
    private void OnWashInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false })
        {
            Close();
        }
    }

    private void Choose(Appearance appearance)
    {
        if (appearance == current)
        {
            return;
        }
        current = appearance;
        AppearanceChosen?.Invoke(appearance);
    }

    private void Choose(ConsentWording wording, bool on)
    {
        Disarm();
        ConsentChosen?.Invoke(wording, on);
    }
}
