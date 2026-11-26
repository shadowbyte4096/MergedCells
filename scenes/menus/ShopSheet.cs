using System;
using Godot;
using MergedCells.Core;

namespace MergedCells;

// One shop item: its name, a big preview on the real board, one line and Close beside its action (Buy, Use, or a greyed
// In use). Opening it was the confirmation, so Buy buys at once
public partial class ShopSheet : EditorSheet
{
    private Label title;
    private BoardPreview preview;
    private Label line;
    private Button action;
    private ShopOffer offer;

    public event Action<ShopOffer> ActionPressed;

    public override void _Ready()
    {
        base._Ready();
        title = GetNode<Label>("Sheet/Column/Title");
        preview = GetNode<BoardPreview>("Sheet/Column/Preview");
        line = GetNode<Label>("Sheet/Column/Line");
        action = GetNode<Button>("Sheet/Column/Buttons/Action");
        GetNode<Button>("Sheet/Column/Buttons/Close").Pressed += Close;
        action.Pressed += () =>
        {
            // EmitSignal and same-frame presses get past Disabled
            if (IsOpen && !action.Disabled)
            {
                ActionPressed?.Invoke(offer);
            }
        };
    }

    public void Open(ShopOffer shown, long balance, BoardTheme board, PlayerLook look)
    {
        offer = shown;
        title.Text = shown.Name;
        var shortBy = ShopOffers.ShortBy(shown, balance);
        (line.Text, action.Text, action.Disabled) = shown.State switch
        {
            OfferState.ForSale when shortBy > 0 => (ShopWords.Short(shortBy), ShopWords.Buy(shown.Price ?? 0), true),
            OfferState.ForSale => (ShopWords.Balance(balance), ShopWords.Buy(shown.Price ?? 0), false),
            OfferState.Owned => (ShopWords.Owned, ShopWords.Use, false),
            OfferState.Free => (ShopWords.Free, ShopWords.Use, false),
            _ => (ShopWords.InUse, ShopWords.InUse, true),
        };
        Show();
        preview.ShowBoard(ShopBoards.Preview, ShopBoards.PreviewPlayer, board, look);
    }

    // A theme or appearance change while the sheet is open: the preview follows
    public void Restyle(BoardTheme board, PlayerLook look)
    {
        if (IsOpen)
        {
            preview.ShowBoard(ShopBoards.Preview, ShopBoards.PreviewPlayer, board, look);
        }
    }
}
