using System;
using Godot;
using MergedCells.Core;

namespace MergedCells;

// The settings card over a wash, in the results card's style. Main wires every screen's sheet (IScreen.Settings): it
// supplies the values the sheet opens on and hears each choice. A choice applies at once and leaves the sheet open; Done,
// a tap on the wash or Esc closes it.
public partial class SettingsSheet : Control
{
    private const string Explanation =
        "Keeps how you play each map on this phone (moves, time and undos, never your name) so the developer can tune difficulty.";

    private Button system;
    private Button light;
    private Button dark;
    private Control playData;
    private CheckButton recordSwitch;
    private Label note;
    private Button export;
    private Label exportResult;
    private Appearance current;

    public event Action<Appearance> AppearanceChosen;

    public event Action<bool> RecordingChosen;

    public event Action ExportPressed;

    public event Action Closed;

    // Set by Main when the screen opens
    public Func<SheetValues> Current { get; set; }

    public bool IsOpen => Visible;

    public override void _Ready()
    {
        system = GetNode<Button>("Sheet/Column/Switch/System");
        light = GetNode<Button>("Sheet/Column/Switch/Light");
        dark = GetNode<Button>("Sheet/Column/Switch/Dark");
        playData = GetNode<Control>("Sheet/Column/PlayData");
        recordSwitch = GetNode<CheckButton>("Sheet/Column/PlayData/RecordSwitch");
        note = GetNode<Label>("Sheet/Column/PlayData/Note");
        export = GetNode<Button>("Sheet/Column/PlayData/Export");
        exportResult = GetNode<Label>("Sheet/Column/PlayData/ExportResult");
        system.Pressed += () => Choose(Appearance.System);
        light.Pressed += () => Choose(Appearance.Light);
        dark.Pressed += () => Choose(Appearance.Dark);
        recordSwitch.Toggled += on => RecordingChosen?.Invoke(on);
        export.Pressed += () => ExportPressed?.Invoke();
        GetNode<Button>("Sheet/Column/Done").Pressed += Close;
        GetNode<Control>("Wash").GuiInput += OnWashInput;
    }

    public void Open()
    {
        exportResult.Hide();
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
        playData.Visible = values.Playtest;
        recordSwitch.SetPressedNoSignal(values.RecordPlayData);
        var recorded = values.Recorded switch
        {
            0 => "Nothing recorded yet.",
            1 => "1 map recorded.",
            _ => $"{values.Recorded} maps recorded.",
        };
        note.Text = $"{Explanation} {recorded}";
        export.Disabled = values.Recorded == 0;
    }

    public void ShowExportResult(string sentence)
    {
        exportResult.Text = sentence;
        exportResult.Show();
    }

    public void Close()
    {
        if (!Visible)
        {
            return;
        }
        Hide();
        Closed?.Invoke();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Visible && @event.IsActionPressed("ui_cancel"))
        {
            GetViewport().SetInputAsHandled();
            Close();
        }
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
}
