using StardewValley;
using StardewValley.Objects;
using SVMouseTweaks.Configuration;

namespace SVMouseTweaks.Reserve;

/// <summary>
/// 预留开关的持久化。Global 模式读本机配置；PerChest 模式按玩家 ID 存在箱子的 modData 里。
/// 不使用地图坐标，推动箱子时 modData 随箱子保留。
/// </summary>
public static class ReserveStateStore
{
    public const string KeyPrefix = "yushu.SVMouseTweaks/Reserve/";

    public static string CurrentPlayerKey => KeyPrefix + Game1.player.UniqueMultiplayerID.ToString();

    public static bool IsPerChestEnabled(Chest chest)
    {
        return chest.modData.TryGetValue(CurrentPlayerKey, out string? value) && value == "1";
    }

    public static bool IsReserveEnabled(Chest chest)
    {
        ModConfig config = ModEntry.Config;
        return config.ReserveMode == ReserveMode.Global
            ? config.ReserveGlobalEnabled
            : IsPerChestEnabled(chest);
    }

    public static int EffectiveReserve(Chest chest, Item item)
    {
        if (!IsReserveEnabled(chest) || !ReserveItemRules.AppliesTo(item,
                ModEntry.Config.ReserveOnlyStackable, ModEntry.Config.ReserveCategories))
        {
            return 0;
        }

        return Math.Clamp(ModEntry.Config.ReserveAmount, 1, 99);
    }

    public static void SetPerChest(Chest chest, bool enabled)
    {
        if (enabled)
        {
            chest.modData[CurrentPlayerKey] = "1";
        }
        else
        {
            chest.modData.Remove(CurrentPlayerKey);
        }
    }

    /// <summary>箱子被破坏、替换或重新放置时清除所有玩家的 PerChest 状态。</summary>
    public static void Clear(Chest chest)
    {
        foreach (string key in chest.modData.Keys.ToList())
        {
            if (key.StartsWith(KeyPrefix, StringComparison.Ordinal))
            {
                chest.modData.Remove(key);
            }
        }
    }
}
