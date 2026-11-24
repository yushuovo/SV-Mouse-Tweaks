using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using SVMouseTweaks.Integrations;
using SVMouseTweaks.Input;
using SVMouseTweaks.Rules;
using SVMouseTweaks.Transfers;

namespace SVMouseTweaks.Inventory;

/// <summary>
/// GameMenu 的 InventoryPage 专用分配会话。
/// 左键：拖动记录有效格，松开时一次提交；右键：每经过一个新有效格放 1。
/// </summary>
public static class DistributionSession
{
    private static InventoryPage? _page;
    private static GameMenu? _menu;
    private static Item? _held;
    private static readonly List<int> Order = new();
    private static readonly HashSet<int> SlotSet = new();
    private static readonly HashSet<int> RightVisited = new();
    private static bool _left;
    private static bool _right;
    private static bool _rightOwnedUntilRelease;
    private static int _lastX;
    private static int _lastY;

    public static bool IsActive => _left || _right;

    public static bool TryHandleLeftClick(InventoryPage page, int x, int y)
    {
        if (!ModEntry.Config.EnableMod)
        {
            return false;
        }

        if (Game1.activeClickableMenu is not GameMenu menu ||
            !ReferenceEquals(menu.GetCurrentPage(), page))
        {
            return false;
        }

        if (Game1.player.CursorSlotItem is not Item held)
        {
            return false;
        }

        int index = page.inventory.getInventoryPositionOfClick(x, y);
        if (index < 0)
        {
            return false;
        }

        if (InputCoordinator.SelectInventoryGesture(ModEntry.Config, rightButton: false) != InventoryGesture.Divide)
        {
            return false;
        }

        if (IsActive && (!ReferenceEquals(_held, held) || !ReferenceEquals(_page, page)))
        {
            Cancel();
        }

        if (_right && IsActive)
        {
            // 右键手势进行中，忽略新的左键按下，避免在同一次按住里切换模式。
            return true;
        }

        bool valid = IsValidSlot(page, index, held);
        if (!IsActive && !valid)
        {
            // 首个格子不能承接该物品时交还原版处理，避免吞掉普通点击。
            return false;
        }

        if (!_left)
        {
            _left = true;
            _right = false;
            _held = held;
            _page = page;
            _menu = menu;
            Order.Clear();
            SlotSet.Clear();
            RightVisited.Clear();
            _lastX = x;
            _lastY = y;
        }

        AddSlot(index);
        return true;
    }

    public static bool TryHandleRightClick(InventoryPage page, int x, int y)
    {
        if (!ModEntry.Config.EnableMod)
        {
            return false;
        }

        if (Game1.activeClickableMenu is not GameMenu menu ||
            !ReferenceEquals(menu.GetCurrentPage(), page))
        {
            return false;
        }

        if (_rightOwnedUntilRelease && !_right)
        {
            // 右键逐格分配把手中物品放完后，原版仍会按 650ms 重复调用右键；
            // 直到松开前吞掉这些重复调用，避免原版把鼠标下的堆拿起来。
            if (!InputCoordinator.IsKeyDown(SButton.MouseRight) ||
                Game1.oldMouseState.RightButton == ButtonState.Released)
            {
                // 已松开（或这是松开后的新一次按下）：不再吞当前点击。
                _rightOwnedUntilRelease = false;
            }
            else
            {
                return true;
            }
        }

        if (Game1.player.CursorSlotItem is not Item held)
        {
            return false;
        }

        int index = page.inventory.getInventoryPositionOfClick(x, y);
        if (index < 0)
        {
            return false;
        }

        if (InputCoordinator.SelectInventoryGesture(ModEntry.Config, rightButton: true) != InventoryGesture.Distribute)
        {
            return false;
        }

        if (IsActive && (!ReferenceEquals(_held, held) || !ReferenceEquals(_page, page)))
        {
            Cancel();
        }

        if (_left && IsActive)
        {
            // 左键均分预览进行中，右键不插入新操作。
            return true;
        }

        bool valid = IsValidSlot(page, index, held);
        if (!IsActive && !valid)
        {
            // 无效首格交还原版；只有已经开始的右键手势才忽略无效格。
            return false;
        }

        if (!_right)
        {
            _left = false;
            _right = true;
            _rightOwnedUntilRelease = true;
            _held = held;
            _page = page;
            _menu = menu;
            Order.Clear();
            SlotSet.Clear();
            RightVisited.Clear();
            _lastX = x;
            _lastY = y;
        }

        PlaceOne(index);
        return true;
    }

    public static void Tick(InventoryPage page)
    {
        if (!IsActive)
        {
            // 会话已结束但右键仍被按住时，清理“吞右键”标记。
            if (_rightOwnedUntilRelease && !InputCoordinator.IsKeyDown(SButton.MouseRight))
            {
                _rightOwnedUntilRelease = false;
            }

            return;
        }

        if (_page is null || _menu is null || _held is null)
        {
            return;
        }

        if (!ReferenceEquals(_page, page) ||
            !ReferenceEquals(Game1.activeClickableMenu, _menu) ||
            !ReferenceEquals(_menu.GetCurrentPage(), page))
        {
            Cancel();
            return;
        }

        if (Game1.player.CursorSlotItem is not Item held || !ReferenceEquals(held, _held))
        {
            Cancel();
            return;
        }

        if (!Game1.game1.IsActive)
        {
            Cancel();
            return;
        }

        int x = Game1.getMouseX();
        int y = Game1.getMouseY();

        if (_left)
        {
            if (!InputCoordinator.IsKeyDown(SButton.MouseLeft))
            {
                Commit();
                Cancel();
                return;
            }

            SamplePath(page, x, y);
        }
        else if (_right)
        {
            if (!InputCoordinator.IsKeyDown(SButton.MouseRight))
            {
                Cancel();
                return;
            }

            SamplePath(page, x, y);
        }

        _lastX = x;
        _lastY = y;
    }

    public static void DrawPreview(SpriteBatch batch, InventoryPage page)
    {
        if (!_left || _held is null || _page is null || !ReferenceEquals(_page, page))
        {
            return;
        }

        DistributionPlan plan = BuildPlan();
        foreach (SlotAddition addition in plan.Slots)
        {
            if (addition.SlotIndex < 0 || addition.SlotIndex >= page.inventory.inventory.Count)
            {
                continue;
            }

            Rectangle bounds = page.inventory.inventory[addition.SlotIndex].bounds;
            var position = new Vector2(bounds.X + 6f, bounds.Y + bounds.Height - 26f);
            Utility.drawTinyDigits(addition.Add, batch, position + new Vector2(1f, 1f), 3f, 0.9f, Color.Black * 0.7f);
            Utility.drawTinyDigits(addition.Add, batch, position, 3f, 0.91f, Color.White);
        }
    }

    public static void OnMenuChanged(IClickableMenu? newMenu)
    {
        if (!IsActive)
        {
            // 会话已结束但可能仍有“吞右键”标记；菜单切换时必须清掉，
            // 否则在新菜单里第一次右键会被误吞。
            _rightOwnedUntilRelease = false;
            return;
        }

        if (newMenu is not null && ReferenceEquals(newMenu, _menu))
        {
            return;
        }

        Cancel();
    }

    public static void Cancel()
    {
        _page = null;
        _menu = null;
        _held = null;
        _left = false;
        _right = false;
        _rightOwnedUntilRelease = false;
        Order.Clear();
        SlotSet.Clear();
        RightVisited.Clear();
    }

    private static void Commit()
    {
        if (_held is null || _page is null || _held.Stack <= 0)
        {
            return;
        }

        DistributionPlan plan = BuildPlan();
        IList<Item> items = _page.inventory.actualInventory;
        int added = 0;

        foreach (SlotAddition addition in plan.Slots)
        {
            added += TransferService.AddToSlot(items, addition.SlotIndex, _held, addition.Add);
        }

        if (added <= 0)
        {
            return;
        }

        _held.Stack -= added;
        if (_held.Stack <= 0)
        {
            Game1.player.CursorSlotItem = null;
        }
    }

    private static DistributionPlan BuildPlan()
    {
        if (_held is null || _page is null)
        {
            return new DistributionPlan(Array.Empty<SlotAddition>(), 0, 0, 0);
        }

        IList<Item> items = _page.inventory.actualInventory;
        var capacities = new List<SlotCapacity>(Order.Count);
        foreach (int slot in Order)
        {
            if (slot < 0 || slot >= items.Count || FavoriteSlots.IsFavorite(slot))
            {
                continue;
            }

            Item? target = items[slot];
            int capacity;
            if (target is null)
            {
                capacity = _held.maximumStackSize();
            }
            else if (target.canStackWith(_held) && target.getRemainingStackSpace() > 0)
            {
                capacity = target.getRemainingStackSpace();
            }
            else
            {
                continue;
            }

            capacities.Add(new SlotCapacity(slot, capacity));
        }

        return DistributionPlanner.Plan(_held.Stack, capacities);
    }

    private static void SamplePath(InventoryPage page, int x, int y)
    {
        int dx = x - _lastX;
        int dy = y - _lastY;
        double distance = Math.Sqrt((double)dx * dx + (double)dy * dy);
        int steps = Math.Max(1, (int)Math.Ceiling(distance / 4.0));
        int lastIndex = -1;

        for (int step = 1; step <= steps; step++)
        {
            int px = _lastX + dx * step / steps;
            int py = _lastY + dy * step / steps;
            int index = page.inventory.getInventoryPositionOfClick(px, py);
            if (index < 0 || index == lastIndex)
            {
                continue;
            }

            lastIndex = index;
            if (_left)
            {
                AddSlot(index);
            }
            else
            {
                PlaceOne(index);
            }
        }
    }

    private static void AddSlot(int index)
    {
        if (_page is null || _held is null || !IsValidSlot(_page, index, _held))
        {
            return;
        }

        if (SlotSet.Add(index))
        {
            Order.Add(index);
        }
    }

    private static void PlaceOne(int index)
    {
        if (_page is null || _held is null || !IsValidSlot(_page, index, _held))
        {
            return;
        }

        if (!RightVisited.Add(index))
        {
            return;
        }

        IList<Item> items = _page.inventory.actualInventory;
        int added = TransferService.AddToSlot(items, index, _held, 1);
        if (added <= 0)
        {
            return;
        }

        _held.Stack -= added;
        if (_held.Stack <= 0)
        {
            Game1.player.CursorSlotItem = null;
            Cancel();

            // 覆盖 Cancel 的清标记：从放完最后一个物品到松开右键之间仍要吞掉原版右键连点。
            _rightOwnedUntilRelease = true;
        }
    }

    private static bool IsValidSlot(InventoryPage page, int index, Item held)
    {
        IList<Item> items = page.inventory.actualInventory;
        if (index < 0 || index >= items.Count || FavoriteSlots.IsFavorite(index))
        {
            return false;
        }

        Item? target = items[index];
        if (target is null)
        {
            return true;
        }

        return target.canStackWith(held) && target.getRemainingStackSpace() > 0;
    }
}
