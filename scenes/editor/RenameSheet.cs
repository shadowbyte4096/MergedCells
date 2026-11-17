using System;
using Godot;

namespace MergedCells;

// The level's name in a text field, with Done. A name the editor refuses keeps the sheet open with a line saying why
public partial class RenameSheet : EditorSheet
{
    private LineEdit field;
    private Label problem;

    public event Action<string> Submitted;

    public override void _Ready()
    {
        base._Ready();
        field = GetNode<LineEdit>("Sheet/Column/Field");
        problem = GetNode<Label>("Sheet/Column/Problem");
        GetNode<Button>("Sheet/Column/Done").Pressed += Submit;
        field.TextSubmitted += _ => Submit();
    }

    public void Open(string name)
    {
        field.Text = name;
        problem.Hide();
        Show();
        field.GrabFocus();
        field.CaretColumn = field.Text.Length;
    }

    public void ShowProblem(string text)
    {
        problem.Text = text;
        problem.Show();
    }

    private void Submit() => Submitted?.Invoke(field.Text);
}
