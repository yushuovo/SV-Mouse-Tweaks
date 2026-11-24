using StardewModdingAPI;
using SVMouseTweaks.Configuration;
using SVMouseTweaks.Inventory;
using SVMouseTweaks.Transfers;

namespace SVMouseTweaks.Input;

public enum ChestGestureAction
{
    None,
    TakeAllOfType,
    SweepAllItems,
    SweepSameType,
}

public enum WheelTier
{
    None,
    Small,
    Medium,
    Large,
}

public enum InventoryGesture
{
    None,
    Divide,
    Distribute,
}

/// <summary>
/// 输入仲裁：场景有效 → 功能开启 → 绑定满足 → 最具体组合 → 固定优先级。
/// 每个场景只会返回一个动作。
/// </summary>
public static class InputCoordinator
{
    public static bool IsKeyDown(SButton key)
    {
        return ModEntry.Instance.Helper.Input.IsDown(key);
    }

    public static ChestGestureAction SelectChestGesture(ModConfig config)
    {
        return SelectChestGesture(config, out _);
    }

    public static ChestGestureAction SelectChestGesture(ModConfig config, out ModifierBinding selectedBinding)
    {
        var candidates = new (ChestGestureAction Action, bool Enabled, string Binding, int Priority)[]
        {
            (ChestGestureAction.TakeAllOfType, config.EnableChestTakeAllOfType, config.ChestTakeAllModifiers, 30),
            (ChestGestureAction.SweepAllItems, config.EnableChestAllItemsSweep, config.ChestAllItemsModifiers, 20),
            (ChestGestureAction.SweepSameType, config.EnableChestSameTypeSweep, config.ChestSameTypeModifiers, 10),
        };

        ChestGestureAction best = ChestGestureAction.None;
        ModifierBinding bestBinding = ModifierBinding.Empty;
        int bestSpecificity = -1;
        int bestPriority = int.MinValue;

        foreach (var candidate in candidates)
        {
            if (!candidate.Enabled)
            {
                continue;
            }

            ModifierBinding binding = ModifierBinding.ParseOrEmpty(candidate.Binding);
            if (!binding.IsSatisfied(IsKeyDown))
            {
                continue;
            }

            if (binding.Specificity > bestSpecificity ||
                (binding.Specificity == bestSpecificity && candidate.Priority > bestPriority))
            {
                best = candidate.Action;
                bestBinding = binding;
                bestSpecificity = binding.Specificity;
                bestPriority = candidate.Priority;
            }
        }

        selectedBinding = bestBinding;
        return best;
    }

    public static (WheelTier Tier, int Amount) SelectWheelTier(ModConfig config)
    {
        var candidates = new (WheelTier Tier, bool Enabled, string Binding, int Amount, int Priority)[]
        {
            (WheelTier.Large, config.EnableWheel, config.WheelLargeModifiers, config.WheelLargeAmount, 30),
            (WheelTier.Medium, config.EnableWheel, config.WheelMediumModifiers, config.WheelMediumAmount, 20),
            (WheelTier.Small, config.EnableWheel, config.WheelSmallModifiers, config.WheelSmallAmount, 10),
        };

        WheelTier bestTier = WheelTier.None;
        int bestAmount = 0;
        int bestPriority = int.MinValue;

        foreach (var candidate in candidates)
        {
            if (!candidate.Enabled)
            {
                continue;
            }

            ModifierBinding binding = ModifierBinding.ParseOrEmpty(candidate.Binding);
            if (!binding.IsSatisfied(IsKeyDown))
            {
                continue;
            }

            if (candidate.Priority > bestPriority)
            {
                bestTier = candidate.Tier;
                bestAmount = candidate.Amount;
                bestPriority = candidate.Priority;
            }
        }

        return (bestTier, bestAmount);
    }

    public static InventoryGesture SelectInventoryGesture(ModConfig config, bool rightButton)
    {
        string bindingText = rightButton ? config.InventoryDistributeModifiers : config.InventoryDivideModifiers;
        bool enabled = rightButton ? config.EnableInventoryDistribute : config.EnableInventoryDivide;
        if (!enabled)
        {
            return InventoryGesture.None;
        }

        return ModifierBinding.ParseOrEmpty(bindingText).IsSatisfied(IsKeyDown)
            ? (rightButton ? InventoryGesture.Distribute : InventoryGesture.Divide)
            : InventoryGesture.None;
    }

    public static bool HasDuplicateChestBindings(ModConfig config, out string actions)
    {
        return TryFindDuplicate(
            new (string Name, bool Enabled, string Binding)[]
            {
                ("全物品扫过", config.EnableChestAllItemsSweep, config.ChestAllItemsModifiers),
                ("同类扫过", config.EnableChestSameTypeSweep, config.ChestSameTypeModifiers),
                ("同类全部转移", config.EnableChestTakeAllOfType, config.ChestTakeAllModifiers),
            },
            out actions);
    }

    public static bool HasDuplicateWheelBindings(ModConfig config, out string actions)
    {
        return TryFindDuplicate(
            new (string Name, bool Enabled, string Binding)[]
            {
                ("滚轮 1", config.EnableWheel, config.WheelSmallModifiers),
                ("滚轮 10", config.EnableWheel, config.WheelMediumModifiers),
                ("滚轮 100", config.EnableWheel, config.WheelLargeModifiers),
            },
            out actions);
    }

    public static bool HasDuplicateInventoryBindings(ModConfig config, out string actions)
    {
        return TryFindDuplicate(
            new (string Name, bool Enabled, string Binding)[]
            {
                ("背包左键均分", config.EnableInventoryDivide, config.InventoryDivideModifiers),
                ("背包右键逐格", config.EnableInventoryDistribute, config.InventoryDistributeModifiers),
            },
            out actions);
    }

    public static void EndAllSessions()
    {
        ChestGestureSession.End(clearNulls: true);
        DistributionSession.Cancel();
        CollectSession.Cancel();
        WheelController.Reset();
    }

    private static bool TryFindDuplicate(
        IReadOnlyList<(string Name, bool Enabled, string Binding)> candidates,
        out string actions)
    {
        var seen = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var candidate in candidates)
        {
            if (!candidate.Enabled)
            {
                continue;
            }

            string key = ModifierBinding.ParseOrEmpty(candidate.Binding).NormalizedKey;
            if (seen.TryGetValue(key, out string? first))
            {
                actions = $"{first} / {candidate.Name}";
                return true;
            }

            seen[key] = candidate.Name;
        }

        actions = string.Empty;
        return false;
    }
}
