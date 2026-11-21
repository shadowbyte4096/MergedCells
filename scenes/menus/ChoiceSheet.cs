using System;
using System.Collections.Generic;
using Godot;

namespace MergedCells;

// A filter's choices on the library screen, one button each, the current one solid; a tap chooses and closes
public partial class ChoiceSheet : EditorSheet
{
    private Label title;
    private Container choices;

    public event Action<int> Chosen;

    public override void _Ready()
    {
        base._Ready();
        title = GetNode<Label>("Sheet/Column/Title");
        choices = GetNode<Container>("Sheet/Column/Choices");
    }

    public void Open(string heading, IReadOnlyList<string> options, int current)
    {
        title.Text = heading;
        foreach (var child in choices.GetChildren())
        {
            child.QueueFree();
        }
        for (var index = 0; index < options.Count; index++)
        {
            var chosen = index;
            var button = new Button
            {
                Text = options[index],
                ThemeTypeVariation = index == current ? "PillPrimary" : "PillButton",
                FocusMode = FocusModeEnum.None,
            };
            button.Pressed += () =>
            {
                Close();
                Chosen?.Invoke(chosen);
            };
            choices.AddChild(button);
        }
        Show();
    }
}
