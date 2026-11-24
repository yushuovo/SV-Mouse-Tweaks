using StardewValley.Objects;

namespace SVMouseTweaks.Transfers;

/// <summary>
/// 本 Mod 只接管会按普通容器展示、可安全取放的箱子。
/// 迷你出货箱、宝石复制机之类不是本 Mod 的目标，交还原版。
/// </summary>
public static class ChestSupport
{
    public static bool IsSupported(Chest chest)
    {
        return chest.SpecialChestType is not (Chest.SpecialChestTypes.MiniShippingBin
            or Chest.SpecialChestTypes.JunimoChest
            or Chest.SpecialChestTypes.Enricher);
    }
}
