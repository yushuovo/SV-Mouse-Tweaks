using StardewModdingAPI;

namespace SVMouseTweaks.Configuration;

public enum ReserveMode
{
    Global,
    PerChest,
}

public sealed class ModConfig
{
    private static readonly HashSet<string> LoggedInvalidFields = new(StringComparer.Ordinal);

    public bool EnableMod { get; set; } = true;

    // 箱子连续转移
    public bool EnableChestSameTypeSweep { get; set; } = true;
    public string ChestSameTypeModifiers { get; set; } = string.Empty;

    public bool EnableChestAllItemsSweep { get; set; } = true;
    public string ChestAllItemsModifiers { get; set; } = "Shift";

    public bool EnableChestTakeAllOfType { get; set; } = true;
    public string ChestTakeAllModifiers { get; set; } = "Ctrl+Shift";

    // 滚轮
    public bool EnableWheel { get; set; } = true;
    public int WheelSmallAmount { get; set; } = 1;
    public int WheelMediumAmount { get; set; } = 10;
    public int WheelLargeAmount { get; set; } = 100;
    public string WheelSmallModifiers { get; set; } = string.Empty;
    public string WheelMediumModifiers { get; set; } = "Shift";
    public string WheelLargeModifiers { get; set; } = "Ctrl+Shift";
    public bool WheelReverse { get; set; }
    public bool WheelOppositeInventorySide { get; set; }

    // 背包分配
    public bool EnableInventoryDivide { get; set; } = true;
    public string InventoryDivideModifiers { get; set; } = string.Empty;

    public bool EnableInventoryDistribute { get; set; } = true;
    public string InventoryDistributeModifiers { get; set; } = string.Empty;

    public bool EnableInventoryCollect { get; set; } = true;

    // 预留
    public ReserveMode ReserveMode { get; set; } = ReserveMode.Global;
    public bool ReserveGlobalEnabled { get; set; }
    public int ReserveAmount { get; set; } = 1;
    public bool ReserveOnlyStackable { get; set; } = true;
    public ReserveCategories ReserveCategories { get; set; } = new();
    public bool ReserveAffectsLeftClick { get; set; }
    public bool ReserveAffectsRightClick { get; set; }
    public bool ReserveAffectsShiftRightClick { get; set; }

    /// <summary>修正越界数量与非法辅助键；返回是否改动了配置。</summary>
    public static bool Normalize(ModConfig config, IMonitor monitor)
    {
        bool changed = false;
        if (config.ReserveCategories is null)
        {
            config.ReserveCategories = new ReserveCategories();
            changed = true;
        }

        int small = config.WheelSmallAmount;
        changed |= Clamp(ref small, 1, 999, "WheelSmallAmount", monitor);
        config.WheelSmallAmount = small;

        int medium = config.WheelMediumAmount;
        changed |= Clamp(ref medium, 1, 999, "WheelMediumAmount", monitor);
        config.WheelMediumAmount = medium;

        int large = config.WheelLargeAmount;
        changed |= Clamp(ref large, 1, 999, "WheelLargeAmount", monitor);
        config.WheelLargeAmount = large;

        int reserve = config.ReserveAmount;
        changed |= Clamp(ref reserve, 1, 99, "ReserveAmount", monitor);
        config.ReserveAmount = reserve;

        if (!Enum.IsDefined(typeof(ReserveMode), config.ReserveMode))
        {
            config.ReserveMode = ReserveMode.Global;
            changed = true;
            WarnOnce("ReserveMode", monitor, "ReserveMode 非法，已恢复为 Global。");
        }

        string chestSameType = config.ChestSameTypeModifiers;
        changed |= NormalizeBinding(ref chestSameType, string.Empty, "ChestSameTypeModifiers", monitor);
        config.ChestSameTypeModifiers = chestSameType;

        string chestAllItems = config.ChestAllItemsModifiers;
        changed |= NormalizeBinding(ref chestAllItems, "Shift", "ChestAllItemsModifiers", monitor);
        config.ChestAllItemsModifiers = chestAllItems;

        string chestTakeAll = config.ChestTakeAllModifiers;
        changed |= NormalizeBinding(ref chestTakeAll, "Ctrl+Shift", "ChestTakeAllModifiers", monitor);
        config.ChestTakeAllModifiers = chestTakeAll;

        string wheelSmall = config.WheelSmallModifiers;
        changed |= NormalizeBinding(ref wheelSmall, string.Empty, "WheelSmallModifiers", monitor);
        config.WheelSmallModifiers = wheelSmall;

        string wheelMedium = config.WheelMediumModifiers;
        changed |= NormalizeBinding(ref wheelMedium, "Shift", "WheelMediumModifiers", monitor);
        config.WheelMediumModifiers = wheelMedium;

        string wheelLarge = config.WheelLargeModifiers;
        changed |= NormalizeBinding(ref wheelLarge, "Ctrl+Shift", "WheelLargeModifiers", monitor);
        config.WheelLargeModifiers = wheelLarge;

        string inventoryDivide = config.InventoryDivideModifiers;
        changed |= NormalizeBinding(ref inventoryDivide, string.Empty, "InventoryDivideModifiers", monitor);
        config.InventoryDivideModifiers = inventoryDivide;

        string inventoryDistribute = config.InventoryDistributeModifiers;
        changed |= NormalizeBinding(ref inventoryDistribute, string.Empty, "InventoryDistributeModifiers", monitor);
        config.InventoryDistributeModifiers = inventoryDistribute;

        return changed;
    }

    private static bool Clamp(ref int value, int min, int max, string field, IMonitor monitor)
    {
        int original = value;
        int clamped = Math.Clamp(original, min, max);
        if (clamped == original)
        {
            return false;
        }

        value = clamped;
        WarnOnce(field, monitor, $"{field}={original} 超出范围，已修正为 {clamped}。");
        return true;
    }

    private static bool NormalizeBinding(ref string value, string fallback, string field, IMonitor monitor)
    {
        if (!ModifierBinding.TryParse(value, out ModifierBinding binding, out string? error))
        {
            value = fallback;
            WarnOnce(field, monitor, $"{field} 配置错误：{error} 已恢复默认值“{fallback}”。");
            return true;
        }

        // 把旧式别名（Shift、Ctrl+Shift、A|B 等）无损迁移成 KeybindList 规范文本。
        string canonical = binding.ToConfigString();
        if (!string.Equals(canonical, value, StringComparison.Ordinal))
        {
            value = canonical;
            return true;
        }

        return false;
    }

    private static void WarnOnce(string field, IMonitor monitor, string message)
    {
        if (LoggedInvalidFields.Add(field))
        {
            monitor.Log(message, LogLevel.Warn);
        }
    }
}
