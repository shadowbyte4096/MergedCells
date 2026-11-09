using Godot;
using MergedCells.Core;

namespace MergedCells;

// One level on the level select: its number in the chapter and, once played, its stars. The current level is solid,
// and an unplayed one fainter. Sizes and styles come from the UI theme's LevelTile entries.
public partial class LevelTile : Button
{
    private const string ThemeType = "LevelTile";

    private Label numberLabel;
    private StarsView starsView;

    public override void _Ready()
    {
        FocusMode = FocusModeEnum.None;
        CustomMinimumSize = new Vector2(GetThemeConstant("width", ThemeType), GetThemeConstant("height", ThemeType));
        var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        column.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        numberLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        starsView = new StarsView { ThemeType = "TileStars", MouseFilter = MouseFilterEnum.Ignore };
        column.AddChild(numberLabel);
        column.AddChild(starsView);
        AddChild(column);
    }

    // Called once the tile is in the tree. stars is null when the level hasn't been finished.
    public void Set(int number, int? stars, bool current)
    {
        numberLabel.Text = number.ToString();
        var state = current ? "Current" : stars is null ? "Unplayed" : "";
        ThemeTypeVariation = ThemeType + state;
        numberLabel.ThemeTypeVariation = "TileNumber" + state;
        starsView.Visible = stars is not null;
        if (stars is { } earned)
        {
            starsView.Looks = StarLooks.For(earned, earned);
        }
    }
}
