using Godot;

namespace MergedCells;

// The light and dark theme files every screen reads, in one place, so all screens switch together
[GlobalClass]
public partial class AppLooks : Resource
{
    [Export] public BoardTheme LightBoard { get; set; }

    [Export] public BoardTheme DarkBoard { get; set; }

    [Export] public PlayerLook LightLook { get; set; }

    [Export] public PlayerLook DarkLook { get; set; }

    [Export] public Theme LightUi { get; set; }

    [Export] public Theme DarkUi { get; set; }

    public BoardTheme Board(bool dark) => dark ? DarkBoard : LightBoard;

    public PlayerLook Look(bool dark) => dark ? DarkLook : LightLook;

    public Theme Ui(bool dark) => dark ? DarkUi : LightUi;
}
