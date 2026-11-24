using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using SVMouseTweaks.Integrations;
using SVMouseTweaks.Input;

namespace SVMouseTweaks.Inventory;

/// <summary>
/// E 背包双击聚拢：普通首击拿起一组后，250ms 内再次左击同一格，
/// 把背包内可堆叠同类聚拢到手持（最多一个最大堆叠），余量留在原槽。
/// 双击优先于左键均分。
/// </summary>
public static class CollectSession
{
    private const long DoubleClickWindowMs = 250;

    private static GameMenu? _menu;
    private static InventoryPage? _page;
    private static int _slot = -1;
    private static long _firstClickTick;
    private static bool _ownedUntilRelease;

    public static bool TryHandleLeftClick(InventoryPage page, int x, int y)
    {
        if (!ModEntry.Config.EnableMod || !ModEntry.Config.EnableInventoryCollect)
        {
            Cancel();
            return false;
        }

        if (Game1.activeClickableMenu is not GameMenu menu ||
            !ReferenceEquals(menu.GetCurrentPage(), page))
        {
            CancelCandidate();
            return false;
        }

        if (_ownedUntilRelease)
        {
            if (InputCoordinator.IsKeyDown(SButton.MouseLeft))
            {
                return true;
            }

            _ownedUntilRelease = false;
        }

        int index = page.inventory.getInventoryPositionOfClick(x, y);
        if (index < 0)
        {
            CancelCandidate();
            return false;
        }

        Item? held = Game1.player.CursorSlotItem;
        if (held is null)
        {
            Item? target = page.inventory.actualInventory[index];
            if (target is null ||
                target.maximumStackSize() <= 1 ||
                FavoriteSlots.IsFavorite(index))
            {
                CancelCandidate();
                return false;
            }

            _menu = menu;
            _page = page;
            _slot = index;
            _firstClickTick = Environment.TickCount64;
            return false;
        }

        if (held.maximumStackSize() <= 1 ||
            _menu is null ||
            _page is null ||
            !ReferenceEquals(_menu, menu) ||
            !ReferenceEquals(_page, page) ||
            _slot != index ||
            Environment.TickCount64 - _firstClickTick > DoubleClickWindowMs)
        {
            CancelCandidate();
            return false;
        }

        Collect(held, page);
        CancelCandidate();
        _ownedUntilRelease = true;
        return true;
    }

    public static void Tick(InventoryPage page)
    {
        if (!ModEntry.Config.EnableMod || !ModEntry.Config.EnableInventoryCollect)
        {
            Cancel();
            return;
        }

        if (_ownedUntilRelease)
        {
            if (InputCoordinator.IsKeyDown(SButton.MouseLeft))
            {
                return;
            }

            _ownedUntilRelease = false;
        }

        if (_page is null || _menu is null)
        {
            return;
        }

        if (!ReferenceEquals(_page, page) ||
            Game1.activeClickableMenu is not GameMenu menu ||
            !ReferenceEquals(menu, _menu) ||
            !ReferenceEquals(menu.GetCurrentPage(), page) ||
            Game1.player.CursorSlotItem is null ||
            Environment.TickCount64 - _firstClickTick > DoubleClickWindowMs)
        {
            CancelCandidate();
            return;
        }

        int hover = page.inventory.getInventoryPositionOfClick(Game1.getMouseX(), Game1.getMouseY());
        if (hover >= 0 && hover != _slot)
        {
            // 长按后扫到其他格：不再视为双击候选。
            CancelCandidate();
        }
    }

    public static void OnMenuChanged(IClickableMenu? newMenu)
    {
        if (newMenu is GameMenu menu && _menu is not null && ReferenceEquals(newMenu, _menu))
        {
            return;
        }

        Cancel();
    }

    public static void Cancel()
    {
        _menu = null;
        _page = null;
        _slot = -1;
        _firstClickTick = 0;
        _ownedUntilRelease = false;
    }

    private static void CancelCandidate()
    {
        _menu = null;
        _page = null;
        _slot = -1;
        _firstClickTick = 0;
    }

    private static void Collect(Item held, InventoryPage page)
    {
        IList<Item> items = page.inventory.actualInventory;
        for (int i = 0; i < items.Count; i++)
        {
            if (held.getRemainingStackSpace() <= 0)
            {
                break;
            }

            if (FavoriteSlots.IsFavorite(i))
            {
                continue;
            }

            Item? source = items[i];
            if (source is null || ReferenceEquals(source, held) || !source.canStackWith(held))
            {
                continue;
            }

            int take = Math.Min(source.Stack, held.getRemainingStackSpace());
            if (take <= 0)
            {
                continue;
            }

            source.Stack -= take;
            held.Stack += take;
            if (source.Stack <= 0)
            {
                items[i] = null!;
            }
        }
    }
}
