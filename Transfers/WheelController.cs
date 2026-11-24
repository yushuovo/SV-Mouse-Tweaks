using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;
using SVMouseTweaks.Input;
using SVMouseTweaks.Reserve;

namespace SVMouseTweaks.Transfers;

/// <summary>
/// 箱子/背包两侧的滚轮转移。向上/向下方向可翻转；一次滚动刻度先归一化，
/// 悬停堆被送空后封锁该格，直到指针离开该格。
/// </summary>
public static class WheelController
{
    private const int Notch = 120;

    private static readonly Dictionary<Chest, HashSet<WheelBlockKey>> Blocked = new();
    private static long _accumulator;
    private static WheelBlockKey? _lastKey;
    private static int _lastSign;

    private readonly record struct WheelBlockKey(bool ChestSide, int Index);

    public static bool TryHandle(IClickableMenu instance, int direction)
    {
        if (!ModEntry.Config.EnableMod || !ModEntry.Config.EnableWheel)
        {
            return false;
        }

        if (instance is not ItemGrabMenu menu ||
            menu.shippingBin ||
            menu.source != 1 ||
            menu.sourceItem is not Chest chest ||
            !ChestSupport.IsSupported(chest))
        {
            return false;
        }

        if (ChestGestureSession.IsActive)
        {
            return false;
        }

        WheelBlockKey? hovered = GetHoveredKey(menu);
        if (hovered is null)
        {
            ClearBlocks(chest);
            _lastKey = null;
            _accumulator = 0;
            return false;
        }

        CleanupBlocks(chest, hovered.Value);

        if (_lastKey != hovered)
        {
            _lastKey = hovered;
            _accumulator = 0;
        }

        int sign = Math.Sign(direction);
        if (sign == 0)
        {
            return true;
        }

        if (_lastSign != 0 && _lastSign != sign)
        {
            _accumulator = 0;
        }

        _lastSign = sign;
        _accumulator += direction;

        int notches = (int)(_accumulator / Notch);
        _accumulator %= Notch;
        if (notches == 0)
        {
            return true;
        }

        notches = Math.Clamp(notches, -100, 100);

        if (Blocked.TryGetValue(chest, out HashSet<WheelBlockKey>? set) && set.Contains(hovered.Value))
        {
            _accumulator = 0;
            return true;
        }

        (WheelTier tier, int amount) = InputCoordinator.SelectWheelTier(ModEntry.Config);
        if (tier == WheelTier.None)
        {
            // 所有档位的辅助键都未满足：不接管这次滚动，把机会交还原版/其他 Mod。
            _accumulator = 0;
            return false;
        }

        amount = Math.Clamp(amount, 1, 999);

        bool send = ModEntry.Config.WheelReverse ? sign > 0 : sign < 0;
        if (ModEntry.Config.WheelOppositeInventorySide && !hovered.Value.ChestSide)
        {
            // 只反转箱子界面里的玩家背包一侧，箱子侧保持原方向。
            send = !send;
        }

        int remainingNotches = Math.Abs(notches);
        for (int i = 0; i < remainingNotches; i++)
        {
            bool moved;
            if (send)
            {
                (int count, bool emptied) = SendFromHovered(menu, chest, hovered.Value, amount);
                moved = count > 0;
                if (emptied)
                {
                    AddBlock(chest, hovered.Value);
                    _accumulator = 0;
                    break;
                }
            }
            else
            {
                moved = PullToHovered(menu, chest, hovered.Value, amount) > 0;
            }

            if (!moved)
            {
                break;
            }
        }

        return true;
    }

    public static void Tick(ItemGrabMenu menu)
    {
        if (menu.source != 1 || menu.sourceItem is not Chest chest)
        {
            return;
        }

        WheelBlockKey? hovered = GetHoveredKey(menu);
        if (hovered is null)
        {
            ClearBlocks(chest);
            _lastKey = null;
            _accumulator = 0;
            return;
        }

        CleanupBlocks(chest, hovered.Value);
        if (_lastKey != hovered)
        {
            _lastKey = hovered;
            _accumulator = 0;
        }
    }

    public static void OnMenuChanged(IClickableMenu? newMenu)
    {
        _accumulator = 0;
        _lastKey = null;
        _lastSign = 0;

        if (newMenu is ItemGrabMenu menu && menu.sourceItem is Chest current)
        {
            foreach (Chest chest in Blocked.Keys.Where(c => !ReferenceEquals(c, current)).ToList())
            {
                Blocked.Remove(chest);
            }
        }
        else
        {
            Blocked.Clear();
        }
    }

    public static void Reset()
    {
        _accumulator = 0;
        _lastKey = null;
        _lastSign = 0;
        Blocked.Clear();
    }

    private static (int Count, bool Emptied) SendFromHovered(
        ItemGrabMenu menu,
        Chest chest,
        WheelBlockKey key,
        int amount)
    {
        IList<Item> chestItems = menu.ItemsToGrabMenu.actualInventory;
        IList<Item> playerItems = menu.inventory.actualInventory;
        IList<Item> sourceItems = key.ChestSide ? chestItems : playerItems;
        IList<Item> targetItems = key.ChestSide ? playerItems : chestItems;

        if (key.Index < 0 || key.Index >= sourceItems.Count)
        {
            return (0, false);
        }

        Item? source = sourceItems[key.Index];
        if (source is null)
        {
            return (0, false);
        }

        int targetCapacity = key.ChestSide ? playerItems.Count : chest.GetActualCapacity();
        // sourceTotal 必须始终是“来源侧该类型的总量”；玩家侧同样需要，
        // 否则 QuantityRules 会把它当成 0 个可用而拒绝转移。
        int reserve = key.ChestSide ? ReserveStateStore.EffectiveReserve(chest, source) : 0;
        long total = TransferService.GetTotal(sourceItems, source, forReserve: reserve > 0);

        int moved = TransferService.Move(
            sourceItems,
            key.Index,
            targetItems,
            targetCapacity,
            amount,
            reserve,
            total);

        return (moved, sourceItems[key.Index] is null);
    }

    private static int PullToHovered(
        ItemGrabMenu menu,
        Chest chest,
        WheelBlockKey key,
        int amount)
    {
        IList<Item> chestItems = menu.ItemsToGrabMenu.actualInventory;
        IList<Item> playerItems = menu.inventory.actualInventory;
        IList<Item> hoveredItems = key.ChestSide ? chestItems : playerItems;
        if (key.Index < 0 || key.Index >= hoveredItems.Count)
        {
            return 0;
        }

        Item? hovered = hoveredItems[key.Index];
        if (hovered is null || hovered.getRemainingStackSpace() <= 0)
        {
            return 0;
        }

        IList<Item> sourceItems = key.ChestSide ? playerItems : chestItems;
        int remaining = amount;
        int movedTotal = 0;

        for (int i = 0; i < sourceItems.Count && remaining > 0; i++)
        {
            Item? source = sourceItems[i];
            if (source is null || ReferenceEquals(source, hovered) || !source.canStackWith(hovered))
            {
                continue;
            }

            // 拉取时来源可能是玩家侧，也可能是箱子侧；sourceTotal 始终取来源侧同类总量。
            int reserve = key.ChestSide ? 0 : ReserveStateStore.EffectiveReserve(chest, source);
            long total = TransferService.GetTotal(sourceItems, source, forReserve: reserve > 0);
            int moved = TransferService.MoveIntoStack(sourceItems, i, hovered, remaining, reserve, total);
            if (moved <= 0)
            {
                break;
            }

            remaining -= moved;
            movedTotal += moved;

            if (hovered.getRemainingStackSpace() <= 0)
            {
                break;
            }
        }

        return movedTotal;
    }

    private static WheelBlockKey? GetHoveredKey(ItemGrabMenu menu)
    {
        int x = Game1.getMouseX();
        int y = Game1.getMouseY();

        int chestIndex = menu.ItemsToGrabMenu.getInventoryPositionOfClick(x, y);
        if (chestIndex >= 0)
        {
            return new WheelBlockKey(true, chestIndex);
        }

        int playerIndex = menu.inventory.getInventoryPositionOfClick(x, y);
        if (playerIndex >= 0)
        {
            return new WheelBlockKey(false, playerIndex);
        }

        return null;
    }

    private static void AddBlock(Chest chest, WheelBlockKey key)
    {
        if (!Blocked.TryGetValue(chest, out HashSet<WheelBlockKey>? set))
        {
            set = new HashSet<WheelBlockKey>();
            Blocked[chest] = set;
        }

        set.Add(key);
    }

    private static void CleanupBlocks(Chest chest, WheelBlockKey hovered)
    {
        if (!Blocked.TryGetValue(chest, out HashSet<WheelBlockKey>? set))
        {
            return;
        }

        set.RemoveWhere(key => key != hovered);
        if (set.Count == 0)
        {
            Blocked.Remove(chest);
        }
    }

    private static void ClearBlocks(Chest chest)
    {
        Blocked.Remove(chest);
    }
}
