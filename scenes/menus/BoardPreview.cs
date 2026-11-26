using Godot;
using MergedCells.Core;

namespace MergedCells;

// The real board drawing in a box: an instance of the board scene (blocks, twists, the start and the player) scaled to
// fit, on the theme's background, so the shop's sheet shows a theme exactly as a level would
public partial class BoardPreview : Control
{
    private const string BoardScene = "res://scenes/board/board.tscn";

    private BoardView board;
    private Map map;
    private Cell player;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        board = GD.Load<PackedScene>(BoardScene).Instantiate<BoardView>();
        AddChild(board);
        board.SetProcess(IsVisibleInTree());
        Resized += Fit;
    }

    // The board works out its view every frame; inside a closed sheet there's nothing to work out
    public override void _Notification(int what)
    {
        if (what == NotificationVisibilityChanged && board is not null)
        {
            board.SetProcess(IsVisibleInTree());
        }
    }

    public void ShowBoard(Map shown, Cell at, BoardTheme theme, PlayerLook look)
    {
        map = shown;
        player = at;
        board.Theme = theme;
        board.Map = shown;
        board.GetNode<StartMarker>("StartMarker").Look = look;
        board.GetNode<StartMarker>("StartMarker").Cell = shown.Start;
        board.GetNode<TrailView>("Trail").Look = look;
        var view = board.GetNode<PlayerView>("Player");
        view.Look = look;
        view.Cell = at;
        QueueRedraw();
        Fit();
    }

    public override void _Draw()
    {
        if (board?.Theme is { } theme)
        {
            DrawRect(new Rect2(Vector2.Zero, Size), theme.Background);
        }
    }

    // Scaled to fit and centred; then the board builds what's on screen now, since a theme set late in a frame would
    // otherwise show one frame of nothing (chunk 9's note)
    private void Fit()
    {
        if (map is null)
        {
            return;
        }
        var bounds = board.Bounds;
        var scale = Mathf.Min(Size.X / bounds.Size.X, Size.Y / bounds.Size.Y);
        board.Scale = new Vector2(scale, scale);
        board.Position = (Size - bounds.Size * scale) / 2 - bounds.Position * scale;
        board.UpdateView();
        board.GetNode<PlayerView>("Player").Cell = player;
    }
}
