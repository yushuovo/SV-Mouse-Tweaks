using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Objects;
using SVMouseTweaks.Reserve;
using SVMouseTweaks.Transfers;

namespace SVMouseTweaks.Patches;

[HarmonyPatch(typeof(Chest), nameof(Chest.clearNulls))]
internal static class ChestClearNullsPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Chest __instance)
    {
        // 手势按住期间跳过压紧；松开后会话结束，下一次 clearNulls 正常执行。
        return !ChestGestureSession.IsProtecting(__instance);
    }
}

[HarmonyPatch(typeof(Chest), nameof(Chest.placementAction))]
internal static class ChestPlacementActionPatch
{
    [HarmonyPostfix]
    private static void Postfix(Chest __instance, bool __result)
    {
        if (__result)
        {
            ReserveStateStore.Clear(__instance);
        }
    }
}

[HarmonyPatch(typeof(Chest), nameof(Chest.performToolAction))]
internal static class ChestPerformToolActionPatch
{
    [HarmonyPostfix]
    private static void Postfix(Chest __instance, bool __result)
    {
        if (__result)
        {
            ReserveStateStore.Clear(__instance);
        }
    }
}

[HarmonyPatch(typeof(Chest), nameof(Chest.destroyAndDropContents))]
internal static class ChestDestroyAndDropContentsPatch
{
    [HarmonyPostfix]
    private static void Postfix(Chest __instance)
    {
        ReserveStateStore.Clear(__instance);
    }
}

[HarmonyPatch(typeof(Chest), nameof(Chest.performObjectDropInAction))]
internal static class ChestPerformObjectDropInActionPatch
{
    [HarmonyPostfix]
    private static void Postfix(Chest __instance, bool __result)
    {
        if (!__result)
        {
            return;
        }

        GameLocation? location = __instance.Location;
        if (location is null)
        {
            return;
        }

        Vector2 tile = __instance.TileLocation;
        if (location.Objects.TryGetValue(tile, out StardewValley.Object? obj) && obj is Chest newChest)
        {
            ReserveStateStore.Clear(newChest);
        }
    }
}
