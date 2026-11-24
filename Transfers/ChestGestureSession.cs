using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;
using SVMouseTweaks.Configuration;
using SVMouseTweaks.Integrations;
using SVMouseTweaks.Input;
using SVMouseTweaks.Reserve;
using SVMouseTweaks.Rules;

namespace SVMouseTweaks.Transfers;

/// <summary>
/// 箱子菜单里的连续转移会话。可从箱子侧或玩家背包侧起手，起手侧锁定方向；
/// 按住期间不调用 clearNulls，来源格保持原位，松开后再清理空位。
/// </summary>
public static class ChestGestureSession
{
    private const int PendingDragThresholdSquared = 16;

    private static Chest? _chest;
    private static IList<Item>? _sourceItems;
    private static IList<Item>? _targetItems;
    private static int _targetCapacity;
    private static bool _sourceIsChest;
    private static Item? _sample;
    private static bool _allItems;
    private static ModifierBinding? _required;
    private static bool _stopped;
    private static bool _pending;
    private static int _pendingIndex;
    private static int _pendingX;
    private static int _pendingY;
    private static bool _bypass;
    private static readonly HashSet<int> Visited = new();
    private static int _lastX;
    private static int _lastY;

    public static bool IsActive => _chest is not null;

    public static bool IsBypassing => _bypass;

    public static bool IsProtecting(Chest chest)
    {
        return _chest is not null && ReferenceEquals(_chest, chest);
    }

    public static bool TryHandleClick(ItemGrabMenu menu, int x, int y)
    {
        if (!ModEntry.Config.EnableMod || IsActive || menu.shippingBin)
        {
            return false;
        }

        if (menu.source != ItemGrabMenu.source_chest ||
            menu.sourceItem is not Chest chest ||
            !ChestSupport.IsSupported(chest))
        {
            return false;
        }

        if (menu.heldItem is not null)
        {
            return false;
        }

        IList<Item> chestItems = menu.ItemsToGrabMenu.actualInventory;
        IList<Item> playerItems = menu.inventory.actualInventory;

        bool sourceIsChest;
        int index;
        int chestIndex = menu.ItemsToGrabMenu.getInventoryPositionOfClick(x, y);
        int playerIndex = menu.inventory.getInventoryPositionOfClick(x, y);
        if (chestIndex >= 0 && chestIndex < chestItems.Count && chestItems[chestIndex] is not null)
        {
            sourceIsChest = true;
            index = chestIndex;
        }
        else if (playerIndex >= 0 && playerIndex < playerItems.Count && playerItems[playerIndex] is not null)
        {
            sourceIsChest = false;
            index = playerIndex;
        }
        else
        {
            return false;
        }

        Item? clicked = sourceIsChest ? chestItems[index] : playerItems[index];
        if (clicked is null)
        {
            return false;
        }

        // 玩家背包侧起手要尊重 Convenient Inventory 的收藏槽；收藏格交还原版。
        if (!sourceIsChest && FavoriteSlots.IsFavorite(index))
        {
            return false;
        }

        ChestGestureAction action = InputCoordinator.SelectChestGesture(ModEntry.Config, out ModifierBinding binding);
        if (action == ChestGestureAction.None)
        {
            return false;
        }

        IList<Item> sourceItems = sourceIsChest ? chestItems : playerItems;
        IList<Item> targetItems = sourceIsChest ? playerItems : chestItems;
        int targetCapacity = sourceIsChest ? playerItems.Count : chest.GetActualCapacity();

        if (action == ChestGestureAction.TakeAllOfType)
        {
            TakeAllOfType(sourceItems, targetItems, targetCapacity, clicked, chest, sourceIsChest);
            return true;
        }

        _chest = chest;
        _sourceIsChest = sourceIsChest;
        _sourceItems = sourceItems;
        _targetItems = targetItems;
        _targetCapacity = targetCapacity;
        _sample = ReserveItemRules.IsStackable(clicked) ? clicked.getOne() : clicked;
        _allItems = action == ChestGestureAction.SweepAllItems;
        _required = binding;
        _stopped = false;
        Visited.Clear();
        _lastX = x;
        _lastY = y;

        // 普通左键必须区分“单击”和“拖动”：先暂存首击，超过 4px 才按拖动处理；
        // 松开仍未拖动时再执行一次原版单击（预留开启且勾选左击限制时改为受限单击）。
        bool needsPending = action == ChestGestureAction.SweepSameType && binding.IsEmpty;
        if (needsPending)
        {
            _pending = true;
            _pendingIndex = index;
            _pendingX = x;
            _pendingY = y;
            return true;
        }

        ProcessSlot(index);
        return true;
    }

    /// <summary>
    /// 预留开启时，可选择让原版单击/Shift+右键也受预留限制。
    /// 返回 true 表示本次右键已被接管（必须阻止原版继续处理）。
    /// </summary>
    public static bool TryHandleReservedRightClick(ItemGrabMenu menu, int x, int y)
    {
        if (!ModEntry.Config.EnableMod || IsActive || menu.shippingBin)
        {
            return false;
        }

        if (menu.source != ItemGrabMenu.source_chest ||
            menu.sourceItem is not Chest chest ||
            !ChestSupport.IsSupported(chest) ||
            menu.heldItem is not null)
        {
            return false;
        }

        bool shift = InputCoordinator.IsKeyDown(SButton.LeftShift) ||
                     InputCoordinator.IsKeyDown(SButton.RightShift);
        bool ctrl = InputCoordinator.IsKeyDown(SButton.LeftControl) ||
                    InputCoordinator.IsKeyDown(SButton.RightControl);
        bool alt = InputCoordinator.IsKeyDown(SButton.LeftAlt) ||
                   InputCoordinator.IsKeyDown(SButton.RightAlt);
        bool win = InputCoordinator.IsKeyDown(SButton.LeftWindows) ||
                   InputCoordinator.IsKeyDown(SButton.RightWindows);

        // Ctrl/Alt/Win 组合交给原版或其他 Mod（CI 的 Ctrl+Shift+右键仍归 CI）。
        if (ctrl || alt || win)
        {
            return false;
        }

        bool handledByReserve = shift
            ? ModEntry.Config.ReserveAffectsShiftRightClick
            : ModEntry.Config.ReserveAffectsRightClick;
        if (!handledByReserve || !ReserveStateStore.IsReserveEnabled(chest))
        {
            return false;
        }

        int index = menu.ItemsToGrabMenu.getInventoryPositionOfClick(x, y);
        IList<Item> chestItems = menu.ItemsToGrabMenu.actualInventory;
        IList<Item> playerItems = menu.inventory.actualInventory;
        if (index < 0 || index >= chestItems.Count)
        {
            return false;
        }

        Item? item = chestItems[index];
        if (item is null)
        {
            return false;
        }

        int reserve = ReserveStateStore.EffectiveReserve(chest, item);
        if (reserve <= 0)
        {
            return false;
        }

        long total = TransferService.GetTotal(chestItems, item, forReserve: true);
        int capacity = (int)TransferService.GetCapacity(playerItems, playerItems.Count, item);
        int requested = shift ? (item.Stack + 1) / 2 : 1;
        int allowed = QuantityRules.Allowed(requested, total, reserve, capacity);
        if (allowed <= 0)
        {
            // 预留不允许再取：吞掉本次右键，避免原版把最后 N 个拿走。
            return true;
        }

        TransferService.Move(
            chestItems,
            index,
            playerItems,
            playerItems.Count,
            requested,
            reserve,
            total);
        chest.clearNulls();
        return true;
    }

    public static void Tick(ItemGrabMenu menu)
    {
        if (_chest is null)
        {
            return;
        }

        if (menu.sourceItem is not Chest chest || !ReferenceEquals(chest, _chest))
        {
            End(clearNulls: true);
            return;
        }

        if (menu.heldItem is not null)
        {
            // 会话期间若原版/其他 Mod 让玩家手持了物品，立即结束批次，避免边拿边倒。
            End(clearNulls: true);
            return;
        }

        if (!InputCoordinator.IsKeyDown(SButton.MouseLeft))
        {
            if (_pending)
            {
                int px = _pendingX;
                int py = _pendingY;
                bool pendingFromChest = _sourceIsChest;
                End(clearNulls: false);
                if (pendingFromChest &&
                    ReserveStateStore.IsReserveEnabled(chest) &&
                    ModEntry.Config.ReserveAffectsLeftClick)
                {
                    RunReservedLeftClick(menu, chest, px, py);
                }
                else
                {
                    RunVanillaClick(menu, px, py);
                }
            }
            else
            {
                bool deposited = !_sourceIsChest;
                End(clearNulls: true);
                if (deposited)
                {
                    ConvenientInventoryCompat.TryOrganizeAfterDeposit(chest);
                }
            }

            return;
        }

        int x = Game1.getMouseX();
        int y = Game1.getMouseY();

        if (_pending)
        {
            int dx = x - _pendingX;
            int dy = y - _pendingY;
            if (dx * dx + dy * dy <= PendingDragThresholdSquared)
            {
                return;
            }

            _pending = false;
            ProcessSlot(_pendingIndex);
            _lastX = _pendingX;
            _lastY = _pendingY;
            SamplePath(menu, chest, x, y);
            _lastX = x;
            _lastY = y;
            return;
        }

        if (_stopped)
        {
            return;
        }

        if (_required is not null && !_required.IsSatisfied(InputCoordinator.IsKeyDown))
        {
            // 辅助键不再匹配：立即停止转移，但空位保护保留到本次按住结束。
            _stopped = true;
            return;
        }

        SamplePath(menu, chest, x, y);
        _lastX = x;
        _lastY = y;
    }

    public static void OnMenuChanged(IClickableMenu? newMenu)
    {
        if (_chest is null)
        {
            return;
        }

        if (newMenu is ItemGrabMenu menu &&
            menu.sourceItem is Chest chest &&
            ReferenceEquals(chest, _chest))
        {
            // ShowMenu() 重建了菜单，但箱子还是同一个；保留会话与空位保护。
            IList<Item> chestItems = menu.ItemsToGrabMenu.actualInventory;
            IList<Item> playerItems = menu.inventory.actualInventory;
            _sourceItems = _sourceIsChest ? chestItems : playerItems;
            _targetItems = _sourceIsChest ? playerItems : chestItems;
            _targetCapacity = _sourceIsChest ? playerItems.Count : chest.GetActualCapacity();
            return;
        }

        End(clearNulls: true);
    }

    public static void End(bool clearNulls)
    {
        Chest? chest = _chest;
        _chest = null;
        _sourceItems = null;
        _targetItems = null;
        _targetCapacity = 0;
        _sourceIsChest = false;
        _sample = null;
        _allItems = false;
        _required = null;
        _stopped = false;
        _pending = false;
        _pendingIndex = 0;
        _pendingX = 0;
        _pendingY = 0;
        Visited.Clear();

        if (clearNulls && chest is not null)
        {
            chest.clearNulls();
        }
    }

    private static void RunVanillaClick(ItemGrabMenu menu, int x, int y)
    {
        _bypass = true;
        try
        {
            menu.receiveLeftClick(x, y, playSound: true);
        }
        finally
        {
            _bypass = false;
        }
    }

    /// <summary>
    /// 预留开启且“原版左击”也受限时，用一次受限转移代替原版整堆拿起。
    /// 只允许拿走超出预留 N 的部分；拿不动时不改变箱子。
    /// </summary>
    private static void RunReservedLeftClick(ItemGrabMenu menu, Chest chest, int x, int y)
    {
        int index = menu.ItemsToGrabMenu.getInventoryPositionOfClick(x, y);
        IList<Item> chestItems = menu.ItemsToGrabMenu.actualInventory;
        IList<Item> playerItems = menu.inventory.actualInventory;
        if (index < 0 || index >= chestItems.Count)
        {
            return;
        }

        Item? item = chestItems[index];
        if (item is null)
        {
            return;
        }

        int reserve = ReserveStateStore.EffectiveReserve(chest, item);
        if (reserve <= 0)
        {
            RunVanillaClick(menu, x, y);
            return;
        }

        long total = TransferService.GetTotal(chestItems, item, forReserve: reserve > 0);
        int capacity = (int)TransferService.GetCapacity(playerItems, playerItems.Count, item);
        int moved = TransferService.Move(
            chestItems,
            index,
            playerItems,
            playerItems.Count,
            item.Stack,
            reserve,
            total);

        if (moved > 0)
        {
            chest.clearNulls();
            Game1.playSound("coin");
        }
    }

    private static void SamplePath(ItemGrabMenu menu, Chest chest, int x, int y)
    {
        int dx = x - _lastX;
        int dy = y - _lastY;
        double distance = Math.Sqrt((double)dx * dx + (double)dy * dy);
        int steps = Math.Max(1, (int)Math.Ceiling(distance / 4.0));
        InventoryMenu container = _sourceIsChest ? menu.ItemsToGrabMenu : menu.inventory;
        int lastIndex = -1;

        for (int step = 1; step <= steps; step++)
        {
            int px = _lastX + dx * step / steps;
            int py = _lastY + dy * step / steps;
            int index = container.getInventoryPositionOfClick(px, py);
            if (index < 0 || index == lastIndex)
            {
                continue;
            }

            lastIndex = index;
            ProcessSlot(index);
        }
    }

    private static void ProcessSlot(int index)
    {
        if (_chest is null || _sourceItems is null || _targetItems is null)
        {
            return;
        }

        if (!Visited.Add(index))
        {
            return;
        }

        if (index < 0 || index >= _sourceItems.Count)
        {
            return;
        }

        if (!_sourceIsChest && FavoriteSlots.IsFavorite(index))
        {
            return;
        }

        Item? item = _sourceItems[index];
        if (item is null)
        {
            return;
        }

        if (!_allItems && (_sample is null || !TransferService.MatchesForTransfer(_sample, item)))
        {
            return;
        }

        int reserve = _sourceIsChest ? ReserveStateStore.EffectiveReserve(_chest, item) : 0;
        long total = TransferService.GetTotal(_sourceItems, item, forReserve: reserve > 0);
        TransferService.Move(
            _sourceItems,
            index,
            _targetItems,
            _targetCapacity,
            item.Stack,
            reserve,
            total);
    }

    private static void TakeAllOfType(
        IList<Item> sourceItems,
        IList<Item> targetItems,
        int targetCapacity,
        Item sample,
        Chest chest,
        bool sourceIsChest)
    {
        Item template = ReserveItemRules.IsStackable(sample) ? sample.getOne() : sample;
        int reserve = sourceIsChest ? ReserveStateStore.EffectiveReserve(chest, sample) : 0;

        for (int i = 0; i < sourceItems.Count; i++)
        {
            if (!sourceIsChest && FavoriteSlots.IsFavorite(i))
            {
                continue;
            }

            Item? candidate = sourceItems[i];
            if (candidate is null || !TransferService.MatchesForTransfer(template, candidate))
            {
                continue;
            }

            long total = TransferService.GetTotal(sourceItems, candidate, forReserve: reserve > 0);
            int moved = TransferService.Move(
                sourceItems,
                i,
                targetItems,
                targetCapacity,
                candidate.Stack,
                reserve,
                total);

            if (moved <= 0)
            {
                break;
            }
        }

        // 单击批量转移不是拖动会话，结束后按原版习惯压紧一次。
        chest.clearNulls();
        if (!sourceIsChest)
        {
            // 玩家→箱子批量存入后，若 CI 自动整理开启，补一次原本会触发的整理。
            ConvenientInventoryCompat.TryOrganizeAfterDeposit(chest);
        }
    }
}
