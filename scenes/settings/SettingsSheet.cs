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

    private Control mainPage;
    private Control privacyPage;
    private Button system;
    private Button light;
    private Button dark;
    private CheckButton playDataSwitch;
    private Label playDataNote;
    private Button export;
    private Label exportResult;
    private CheckButton leaderboardsSwitch;
    private Label cantChange;
    private string cantChangeText;
    private Button delete;
    private string deleteText;
    private Label deleteResult;
    private Appearance current;
    private bool deleteArmed;

    public event Action<Appearance> AppearanceChosen;

    public event Action<ConsentWording, bool> ConsentChosen;

    public event Action ExportPressed;

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
        system = GetNode<Button>("Sheet/Column/Switch/System");
        light = GetNode<Button>("Sheet/Column/Switch/Light");
        dark = GetNode<Button>("Sheet/Column/Switch/Dark");
        playDataSwitch = GetNode<CheckButton>("Sheet/Privacy/PlayDataSwitch");
        playDataNote = GetNode<Label>("Sheet/Privacy/PlayDataNote");
        export = GetNode<Button>("Sheet/Privacy/Export");
        exportResult = GetNode<Label>("Sheet/Privacy/ExportResult");
        leaderboardsSwitch = GetNode<CheckButton>("Sheet/Privacy/LeaderboardsSwitch");
        var leaderboardsNote = GetNode<Label>("Sheet/Privacy/LeaderboardsNote");
        cantChange = GetNode<Label>("Sheet/Privacy/CantChange");
        cantChangeText = cantChange.Text;
        delete = GetNode<Button>("Sheet/Privacy/Delete");
        deleteText = delete.Text;
        deleteResult = GetNode<Label>("Sheet/Privacy/DeleteResult");

        // The texts that go with each kind's answer live with its wording version in Core
        playDataSwitch.Text = ConsentWordings.PlayData.SwitchLabel;
        leaderboardsSwitch.Text = ConsentWordings.Leaderboards.SwitchLabel;
        leaderboardsNote.Text = ConsentWordings.Leaderboards.SwitchNote;
        leaderboardsSwitch.Visible = ConsentWordings.Leaderboards.Offered;
        leaderboardsNote.Visible = ConsentWordings.Leaderboards.Offered;

        system.Pressed += () => Choose(Appearance.System);
        light.Pressed += () => Choose(Appearance.Light);
        dark.Pressed += () => Choose(Appearance.Dark);
        GetNode<Button>("Sheet/Column/PrivacyRow").Pressed += () => ShowPage(privacy: true);
        GetNode<Button>("Sheet/Privacy/Header/Back").Pressed += () => ShowPage(privacy: false);
        playDataSwitch.Toggled += on => Choose(ConsentWordings.PlayData, on);
        leaderboardsSwitch.Toggled += on => Choose(ConsentWordings.Leaderboards, on);
        export.Pressed += () =>
        {
            Disarm();
            ExportPressed?.Invoke();
        };
        delete.Pressed += OnDeletePressed;
        GetNode<Button>("Sheet/Privacy/Policy").Pressed += () =>
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

    // Reads the values again, as after a choice that changes the record count
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
        playDataSwitch.Disabled = !values.CanChange;
        leaderboardsSwitch.Disabled = !values.CanChange;
        var recorded = values.Recorded switch
        {
            0 => "Nothing recorded yet.",
            1 => "1 map recorded.",
            _ => $"{values.Recorded} maps recorded.",
        };
        playDataNote.Text = $"{ConsentWordings.PlayData.SwitchNote} {recorded}";
        export.Disabled = values.Recorded == 0;
        // A save that can't be written at all, or the last write that failed: an answer or deletion then isn't kept
        cantChange.Text = values.CanChange ? values.SaveProblem : cantChangeText;
        cantChange.Visible = !values.CanChange || values.SaveProblem != null;
        delete.Visible = values.HasData;
        delete.Disabled = !values.CanChange;
        Disarm();
    }

    public void ShowExportResult(string sentence)
    {
        exportResult.Text = sentence;
        exportResult.Show();
    }

    public void ShowDeleteResult(string sentence)
    {
        deleteResult.Text = sentence;
        deleteResult.Show();
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
        exportResult.Hide();
        deleteResult.Hide();
        mainPage.Visible = !privacy;
        privacyPage.Visible = privacy;
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
