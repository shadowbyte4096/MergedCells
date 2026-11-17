using System;
using System.Globalization;
using Godot;

namespace MergedCells;

// A board's width and height, and Clear, which asks "Tap again to clear" first: the editor's ⋯ menu, and without Clear
// the custom levels list's New level sheet. The scene sets the title and the confirm button's text
public partial class BoardSheet : EditorSheet
{
    private Label widthValue;
    private Label heightValue;
    private Button widthLess;
    private Button widthMore;
    private Button heightLess;
    private Button heightMore;
    private Button clear;
    private bool clearArmed;
    private int rows;
    private int columns;
    private int minSide;
    private int maxSide;

    public event Action<int, int> ResizePressed;

    public event Action ClearConfirmed;

    [Export] public bool CanClear { get; set; } = true;

    public override void _Ready()
    {
        base._Ready();
        widthValue = GetNode<Label>("Sheet/Column/Width/Value");
        heightValue = GetNode<Label>("Sheet/Column/Height/Value");
        widthLess = GetNode<Button>("Sheet/Column/Width/Less");
        widthMore = GetNode<Button>("Sheet/Column/Width/More");
        heightLess = GetNode<Button>("Sheet/Column/Height/Less");
        heightMore = GetNode<Button>("Sheet/Column/Height/More");
        clear = GetNode<Button>("Sheet/Column/Clear");
        widthLess.Pressed += () => Step(0, -1);
        widthMore.Pressed += () => Step(0, 1);
        heightLess.Pressed += () => Step(-1, 0);
        heightMore.Pressed += () => Step(1, 0);
        GetNode<Button>("Sheet/Column/Resize").Pressed += () => ResizePressed?.Invoke(rows, columns);
        clear.Pressed += OnClearPressed;
    }

    public void Open(int rows, int columns, int minSide, int maxSide)
    {
        this.rows = rows;
        this.columns = columns;
        this.minSide = minSide;
        this.maxSide = maxSide;
        clearArmed = false;
        clear.Text = "Clear";
        clear.Visible = CanClear;
        Refresh();
        Show();
    }

    // A copied level can be outside the limits (chapter 1's Slide is 1×5); a step brings only its own side inside
    private void Step(int rowStep, int columnStep)
    {
        if (rowStep != 0)
        {
            rows = Math.Clamp(rows + rowStep, minSide, maxSide);
        }
        if (columnStep != 0)
        {
            columns = Math.Clamp(columns + columnStep, minSide, maxSide);
        }
        Refresh();
    }

    private void Refresh()
    {
        widthValue.Text = columns.ToString(CultureInfo.InvariantCulture);
        heightValue.Text = rows.ToString(CultureInfo.InvariantCulture);
        widthLess.Disabled = columns <= minSide;
        widthMore.Disabled = columns >= maxSide;
        heightLess.Disabled = rows <= minSide;
        heightMore.Disabled = rows >= maxSide;
    }

    private void OnClearPressed()
    {
        if (!clearArmed)
        {
            clearArmed = true;
            clear.Text = "Tap again to clear";
            return;
        }
        ClearConfirmed?.Invoke();
    }
}
