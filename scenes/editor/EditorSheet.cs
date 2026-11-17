using System;
using Godot;

namespace MergedCells;

// A card over a wash, in the settings sheet's style: a tap on the wash or Esc closes it. Each sheet's scene has a "Wash"
// panel and its card under "Sheet"
public partial class EditorSheet : Control
{
    public event Action Closed;

    public bool IsOpen => Visible;

    public override void _Ready()
    {
        GetNode<Control>("Wash").GuiInput += OnWashInput;
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

    // On release rather than press: closing on the press would let the same touch's drag reach the board as a stroke
    private void OnWashInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false })
        {
            Close();
        }
    }
}
