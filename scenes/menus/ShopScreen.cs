using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MergedCells.Core;

namespace MergedCells;

// The shop: themes and player looks in one list, each row opening a sheet to buy or use it. While a theme's sheet is
// open the whole screen wears that theme, so the player sees its menus before buying (the developer's choice). Holds
// no save: Main buys, chooses and hands the new rows back through Refresh
public partial class ShopScreen : CanvasLayer, IScreen
{
    private readonly List<(LevelPictureView Picture, CosmeticTheme Theme, CosmeticLook Look)> pictures = new();
    private Control root;
    private Control topBar;
    private Control bottomPad;
    private VBoxContainer list;
    private Label balanceLabel;
    private ShopSheet sheet;
    private bool dark;
    private bool dragged;
    private Vector2 pressedAt;
    private Cosmetics catalogue;
    private IReadOnlyList<ShopOffer> themes = [];
    private IReadOnlyList<ShopOffer> looks = [];
    private long balance;
    private ShopOffer sheetOffer;
    private bool sheetIsTheme;
    // The theme whose sheet is open, which the screen wears until the sheet closes
    private CosmeticTheme trying;

    public event Action BackPressed;

    public event Action<string> BuyPressed;

    public event Action<string> UsePressed;

    [Export] public AppLooks Looks { get; set; }

    public bool Dark
    {
        set
        {
            dark = value;
            ApplyLook();
        }
    }

    private CosmeticTheme WornTheme => catalogue.ThemeFor(themes.FirstOrDefault(offer => offer.State == OfferState.InUse)?.Id);

    private CosmeticLook WornLook => catalogue.LookFor(looks.FirstOrDefault(offer => offer.State == OfferState.InUse)?.Id);

    public override void _Ready()
    {
        root = GetNode<Control>("Root");
        topBar = GetNode<Control>("Root/Layout/TopBar");
        bottomPad = GetNode<Control>("Root/Layout/BottomPad");
        list = GetNode<VBoxContainer>("Root/Layout/Scroll/Margin/List");
        balanceLabel = GetNode<Label>("Root/Layout/TopBar/Row/Balance/BalanceLabel");
        sheet = GetNode<ShopSheet>("Root/Sheet");
        GetNode<Button>("Root/Layout/TopBar/Row/Back").Pressed += () => BackPressed?.Invoke();
        sheet.ActionPressed += offer =>
        {
            if (offer.State == OfferState.ForSale)
            {
                BuyPressed?.Invoke(offer.Id);
            }
            else
            {
                UsePressed?.Invoke(offer.Id);
            }
        };
        sheet.Closed += () =>
        {
            trying = null;
            sheetOffer = null;
            ApplyLook();
        };
        GetViewport().SizeChanged += ApplySafeArea;
        ApplyLook();
        ApplySafeArea();
    }

    public void Open(Cosmetics shopCatalogue, IReadOnlyList<ShopOffer> themeOffers, IReadOnlyList<ShopOffer> lookOffers, long coins)
    {
        catalogue = shopCatalogue;
        Refresh(themeOffers, lookOffers, coins);
        GD.Print("Screen: shop");
    }

    // After a purchase or a choice: the sheet closes, putting the screen in what the player now wears
    public void Refresh(IReadOnlyList<ShopOffer> themeOffers, IReadOnlyList<ShopOffer> lookOffers, long coins)
    {
        themes = themeOffers;
        looks = lookOffers;
        balance = coins;
        sheet.Close();
        balanceLabel.Text = CoinWords.Balance(coins);
        foreach (var child in list.GetChildren())
        {
            child.QueueFree();
        }
        pictures.Clear();
        list.AddChild(Section("Themes"));
        foreach (var offer in themes)
        {
            list.AddChild(Row(offer, catalogue.ThemeFor(offer.Id), null));
        }
        list.AddChild(Section("Player looks"));
        foreach (var offer in looks)
        {
            list.AddChild(Row(offer, null, catalogue.LookFor(offer.Id)));
        }
        ApplyLook();
    }

    public bool CloseOverlay()
    {
        var open = sheet.IsOpen;
        sheet.Close();
        return open;
    }

    // Read before the rows, so a touch that scrolled the list doesn't also open the row it ends on (the custom levels
    // list's rule: Godot sends the button its own emulated mouse release)
    public override void _Input(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventScreenTouch { Pressed: true } touch:
                Down(touch.Position);
                break;
            case InputEventScreenDrag drag:
                Moved(drag.Position);
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } press
                when press.Device != InputEvent.DeviceIdEmulation:
                Down(press.Position);
                break;
            case InputEventMouseMotion motion
                when motion.Device != InputEvent.DeviceIdEmulation && motion.ButtonMask.HasFlag(MouseButtonMask.Left):
                Moved(motion.Position);
                break;
        }
    }

    private void Down(Vector2 at)
    {
        pressedAt = at;
        dragged = false;
    }

    private void Moved(Vector2 at) => dragged = dragged || pressedAt.DistanceTo(at) > GameConfigFile.Current.Input.SwipeThreshold;

    private static Label Section(string text) => new()
    {
        Text = text,
        Uppercase = true,
        ThemeTypeVariation = "ShopSection",
        MouseFilter = Control.MouseFilterEnum.Ignore,
    };

    // A theme's row shows its own theme; a look's row shows the look up close in the theme on screen
    private MarginContainer Row(ShopOffer offer, CosmeticTheme theme, CosmeticLook look)
    {
        var picture = new LevelPictureView
        {
            Map = theme is null ? ShopBoards.CloseUp : ShopBoards.Preview,
            Player = theme is null ? ShopBoards.CloseUpPlayer : ShopBoards.PreviewPlayer,
            ShowMarkers = theme is not null,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        pictures.Add((picture, theme, look));

        var column = new VBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        column.AddChild(Text(offer.Name, "RowName", clip: true));
        var note = offer.State switch
        {
            OfferState.Free => ShopWords.Free,
            OfferState.Owned => ShopWords.Owned,
            OfferState.InUse when catalogue.IsFree(offer.Id) => ShopWords.Free,
            _ => null,
        };
        if (note is not null)
        {
            column.AddChild(Text(note, "RowMeta"));
        }

        var content = new HBoxContainer { ThemeTypeVariation = "PictureRow", MouseFilter = Control.MouseFilterEnum.Ignore };
        content.AddChild(picture);
        content.AddChild(column);
        content.AddChild(Tag(offer));
        var panel = new PanelContainer { ThemeTypeVariation = "ListRowPanel", MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddChild(content);
        return LevelRows.Pressable(panel, () => OpenSheet(offer, theme is not null), () => dragged);
    }

    // The price with a coin while for sale, a solid "Use" tag when it can be put on, "In use" for the one worn
    private static Control Tag(ShopOffer offer)
    {
        if (offer.State == OfferState.ForSale)
        {
            var price = new HBoxContainer
            {
                ThemeTypeVariation = "TotalRow",
                MouseFilter = Control.MouseFilterEnum.Ignore,
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            };
            price.AddChild(new CoinView { ThemeType = "PriceCoin", MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter });
            price.AddChild(Text(CoinWords.Balance(offer.Price ?? 0), "RowStats"));
            return price;
        }
        var tag = offer.State == OfferState.InUse ? Text(ShopWords.InUse, "RowMeta") : Text(ShopWords.Use, "YoursTag");
        tag.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        return tag;
    }

    // Only the name clips: a tag that clips has no minimum width, and its box shrinks to nothing
    private static Label Text(string text, string variation, bool clip = false) => new()
    {
        Text = text,
        ThemeTypeVariation = variation,
        MouseFilter = Control.MouseFilterEnum.Ignore,
        ClipText = clip,
        TextOverrunBehavior = clip ? TextServer.OverrunBehavior.TrimEllipsis : TextServer.OverrunBehavior.NoTrimming,
    };

    private void OpenSheet(ShopOffer offer, bool isTheme)
    {
        sheetOffer = offer;
        sheetIsTheme = isTheme;
        trying = isTheme ? catalogue.ThemeFor(offer.Id) : null;
        ApplyLook();
        var (board, look) = SheetLooks();
        sheet.Open(offer, balance, board, look);
    }

    // A theme's sheet shows the theme with the look worn; a look's sheet the look in the theme worn
    private (BoardTheme Board, PlayerLook Look) SheetLooks() =>
        sheetIsTheme
            ? (CosmeticLooks.Board(trying, dark), CosmeticLooks.Look(trying, WornLook, dark))
            : (CosmeticLooks.Board(WornTheme, dark), CosmeticLooks.Look(WornTheme, catalogue.LookFor(sheetOffer.Id), dark));

    private void ApplyLook()
    {
        if (root is null)
        {
            return;
        }
        root.Theme = trying is null ? Looks.Ui(dark) : CosmeticLooks.Menu(trying, dark);
        RenderingServer.SetDefaultClearColor(trying is null ? Looks.Board(dark).Background : CosmeticLooks.Board(trying, dark).Background);
        if (catalogue is null)
        {
            return;
        }
        var shown = trying ?? WornTheme;
        foreach (var (picture, theme, look) in pictures)
        {
            picture.Board = CosmeticLooks.Board(theme ?? shown, dark);
            picture.Look = CosmeticLooks.Look(theme ?? shown, look ?? WornLook, dark);
        }
        if (sheetOffer is not null)
        {
            var (board, playerLook) = SheetLooks();
            sheet.Restyle(board, playerLook);
        }
    }

    private void ApplySafeArea()
    {
        var (top, bottom) = SafeArea.Insets(this);
        topBar.GetParent<Control>().OffsetTop = top;
        bottomPad.CustomMinimumSize = new Vector2(0, bottom);
    }
}
