using StardewValley;
using SVMouseTweaks.Rules;
using SVMouseTweaks.Reserve;

namespace SVMouseTweaks.Transfers;

/// <summary>
/// 对真实物品列表的安全转移。所有操作直接落在 IList&lt;Item&gt; 上，
/// 不复制库存、不调用 clearNulls，因此不会在拖动期间压紧来源格。
/// </summary>
public static class TransferService
{
    /// <summary>来源自身可被移动，但不可堆叠的不同实例不能据类型或名称归为一组。</summary>
    public static bool MatchesForTransfer(Item sample, Item candidate)
    {
        return ReferenceEquals(sample, candidate) || sample.canStackWith(candidate);
    }

    public static long GetCapacity(IList<Item> target, int capacitySlots, Item sample)
    {
        int maxStack = ReserveItemRules.IsStackable(sample) ? Math.Max(1, sample.maximumStackSize()) : 1;
        long total = 0;

        for (int i = 0; i < target.Count; i++)
        {
            Item? item = target[i];
            if (item is null)
            {
                total += maxStack;
            }
            else if (item.canStackWith(sample))
            {
                total += Math.Max(0, item.getRemainingStackSpace());
            }
        }

        for (int i = target.Count; i < capacitySlots; i++)
        {
            total += maxStack;
        }

        return total;
    }

    public static long GetTotal(IList<Item> items, Item sample, bool forReserve = false)
    {
        long total = 0;
        Func<Item, bool> matches = forReserve
            ? ReserveItemRules.CreateReserveMatcher(sample)
            : candidate => MatchesForTransfer(sample, candidate);
        foreach (Item? item in items)
        {
            if (item is not null && matches(item))
            {
                total += item.Stack;
            }
        }

        return total;
    }

    public static int Move(
        IList<Item> sourceItems,
        int sourceIndex,
        IList<Item> targetItems,
        int targetCapacitySlots,
        int requested,
        int reserve,
        long sourceTotal)
    {
        if (sourceIndex < 0 || sourceIndex >= sourceItems.Count)
        {
            return 0;
        }

        Item? source = sourceItems[sourceIndex];
        if (source is null)
        {
            return 0;
        }

        if (!ReserveItemRules.IsStackable(source))
        {
            // 范围策略由调用者决定有效预留；独立物品仍移动原实例，绝不合并。
            if (source.Stack <= 0 || QuantityRules.Allowed(requested, sourceTotal, reserve, 1) <= 0)
            {
                return 0;
            }

            for (int i = 0; i < targetItems.Count && i < targetCapacitySlots; i++)
            {
                if (targetItems[i] is not null) continue;
                targetItems[i] = source;
                sourceItems[sourceIndex] = null!;
                return 1;
            }

            if (targetItems.Count < targetCapacitySlots)
            {
                targetItems.Add(source);
                sourceItems[sourceIndex] = null!;
                return 1;
            }

            return 0;
        }

        long capacity = GetCapacity(targetItems, targetCapacitySlots, source);
        int allowed = QuantityRules.Allowed(
            requested,
            sourceTotal,
            reserve,
            (int)Math.Min(capacity, int.MaxValue));

        // sourceTotal 是整个来源侧的同类总量，可能大于当前来源堆；
        // 单次移动不能超过这一堆实际持有的数量。
        allowed = Math.Min(allowed, Math.Max(0, source.Stack));
        if (allowed <= 0)
        {
            return 0;
        }

        var existing = new List<StackTarget>();
        for (int i = 0; i < targetItems.Count; i++)
        {
            Item? item = targetItems[i];
            if (item is not null && item.canStackWith(source))
            {
                existing.Add(new StackTarget(i, item.Stack));
            }
        }

        var emptySlots = new List<int>();
        for (int i = 0; i < targetItems.Count; i++)
        {
            if (targetItems[i] is null)
            {
                emptySlots.Add(i);
            }
        }

        for (int i = targetItems.Count; i < targetCapacitySlots; i++)
        {
            emptySlots.Add(i);
        }

        TransferAllocation allocation = StackTransferPlanner.Plan(
            allowed,
            source.maximumStackSize(),
            existing,
            emptySlots);

        int moved = 0;

        foreach (MergePlan merge in allocation.Merges)
        {
            if (merge.Index < 0 || merge.Index >= targetItems.Count)
            {
                continue;
            }

            Item? target = targetItems[merge.Index];
            if (target is null || !target.canStackWith(source))
            {
                continue;
            }

            Item portion = source.getOne();
            portion.Stack = merge.Add;
            int overflow = target.addToStack(portion);
            int accepted = merge.Add - overflow;
            if (accepted <= 0)
            {
                break;
            }

            moved += accepted;
            if (overflow > 0)
            {
                break;
            }
        }

        foreach (NewStackPlan newStack in allocation.NewStacks)
        {
            if (moved >= allowed)
            {
                break;
            }

            int stack = Math.Min(newStack.Stack, allowed - moved);
            if (stack <= 0)
            {
                break;
            }

            Item clone = source.getOne();
            clone.Stack = stack;

            if (newStack.SlotIndex >= 0 && newStack.SlotIndex < targetItems.Count)
            {
                if (targetItems[newStack.SlotIndex] is not null)
                {
                    continue;
                }

                targetItems[newStack.SlotIndex] = clone;
            }
            else
            {
                targetItems.Add(clone);
            }

            moved += stack;
        }

        if (moved <= 0)
        {
            return 0;
        }

        source.Stack -= moved;
        if (source.Stack <= 0)
        {
            sourceItems[sourceIndex] = null!;
        }

        return moved;
    }

    /// <summary>只填入一个指定目标堆，不向其他格溢出。用于滚轮“拉取”。</summary>
    public static int MoveIntoStack(
        IList<Item> sourceItems,
        int sourceIndex,
        Item target,
        int requested,
        int reserve,
        long sourceTotal)
    {
        if (sourceIndex < 0 || sourceIndex >= sourceItems.Count)
        {
            return 0;
        }

        Item? source = sourceItems[sourceIndex];
        if (source is null || ReferenceEquals(source, target) || !target.canStackWith(source))
        {
            return 0;
        }

        int space = Math.Max(0, target.getRemainingStackSpace());
        int allowed = QuantityRules.Allowed(requested, sourceTotal, reserve, space);
        allowed = Math.Min(allowed, Math.Max(0, source.Stack));
        if (allowed <= 0)
        {
            return 0;
        }

        Item portion = source.getOne();
        portion.Stack = allowed;
        int overflow = target.addToStack(portion);
        int accepted = allowed - overflow;
        if (accepted <= 0)
        {
            return 0;
        }

        source.Stack -= accepted;
        if (source.Stack <= 0)
        {
            sourceItems[sourceIndex] = null!;
        }

        return accepted;
    }

    /// <summary>把 sample 的 amount 个追加到指定格；空格会新建堆。返回实际追加量。</summary>
    public static int AddToSlot(IList<Item> items, int slotIndex, Item sample, int amount)
    {
        if (amount <= 0 || slotIndex < 0 || slotIndex > items.Count)
        {
            return 0;
        }

        Item? target = slotIndex < items.Count ? items[slotIndex] : null;
        if (target is null)
        {
            Item clone = sample.getOne();
            clone.Stack = Math.Min(amount, Math.Max(1, clone.maximumStackSize()));
            if (slotIndex < items.Count)
            {
                items[slotIndex] = clone;
            }
            else
            {
                items.Add(clone);
            }

            return clone.Stack;
        }

        if (!target.canStackWith(sample))
        {
            return 0;
        }

        int space = Math.Max(0, target.getRemainingStackSpace());
        int add = Math.Min(amount, space);
        if (add <= 0)
        {
            return 0;
        }

        Item portion = sample.getOne();
        portion.Stack = add;
        int overflow = target.addToStack(portion);
        return add - overflow;
    }
}
