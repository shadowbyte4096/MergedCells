using System;
using Godot;
using MergedCells.Core;

namespace MergedCells;

// The settings card over a wash, in the results card's style. Picking an appearance applies at once and leaves the
// sheet open; Done, a tap on the wash or Esc closes it.
public partial class SettingsSheet : Control
{
    private Button system;
    private Button light;
    private Button dark;
    private Appearance current;

    public event Action<Appearance> AppearanceChosen;

    public event Action Closed;

    public bool IsOpen => Visible;

    public override void _Ready()
    {
        system = GetNode<Button>("Sheet/Column/Switch/System");
        light = GetNode<Button>("Sheet/Column/Switch/Light");
        dark = GetNode<Button>("Sheet/Column/Switch/Dark");
        system.Pressed += () => Choose(Appearance.System);
        light.Pressed += () => Choose(Appearance.Light);
        dark.Pressed += () => Choose(Appearance.Dark);
        GetNode<Button>("Sheet/Column/Done").Pressed += Close;
        GetNode<Control>("Wash").GuiInput += OnWashInput;
    }

    public void Open(Appearance appearance)
    {
        current = appearance;
        // Without the signal, so opening never counts as a choice
        system.SetPressedNoSignal(appearance == Appearance.System);
        light.SetPressedNoSignal(appearance == Appearance.Light);
        dark.SetPressedNoSignal(appearance == Appearance.Dark);
        Show();
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
