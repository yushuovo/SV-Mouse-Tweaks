using StardewValley;

namespace SVMouseTweaks.Integrations;

/// <summary>
/// 读取 Convenient Inventory 的收藏槽标记（按玩家 modData 存 0/1 字符串），
/// 这样不必在编译期依赖 CI 程序集。
/// </summary>
public static class FavoriteSlots
{
    public const string ModDataKey = "ConvenientInventory/favoriteItemSlots";

    public static bool IsFavorite(int slotIndex)
    {
        if (slotIndex < 0)
        {
            return false;
        }

        if (!Game1.player.modData.TryGetValue(ModDataKey, out string? value) ||
            string.IsNullOrEmpty(value))
        {
            return false;
        }

        return slotIndex < value.Length && value[slotIndex] == '1';
    }
}
