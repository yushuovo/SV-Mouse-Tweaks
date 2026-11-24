using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;
using StardewValley.Menus;
using SVMouseTweaks.Inventory;

namespace SVMouseTweaks.Patches;

[HarmonyPatch(typeof(InventoryPage))]
internal static class InventoryPagePatches
{
    [HarmonyPatch(nameof(InventoryPage.receiveLeftClick))]
    [HarmonyPrefix]
    private static bool ReceiveLeftClickPrefix(InventoryPage __instance, int x, int y)
    {
        if (!ModEntry.Config.EnableMod)
        {
            return true;
        }

        if (CollectSession.TryHandleLeftClick(__instance, x, y))
        {
            return false;
        }

        return !DistributionSession.TryHandleLeftClick(__instance, x, y);
    }

    [HarmonyPatch(nameof(InventoryPage.receiveRightClick))]
    [HarmonyPrefix]
    private static bool ReceiveRightClickPrefix(InventoryPage __instance, int x, int y)
    {
        if (!ModEntry.Config.EnableMod)
        {
            return true;
        }

        CollectSession.Cancel();
        return !DistributionSession.TryHandleRightClick(__instance, x, y);
    }

    [HarmonyPatch(nameof(InventoryPage.draw))]
    [HarmonyPostfix]
    private static void DrawPostfix(InventoryPage __instance, SpriteBatch b)
    {
        if (!ModEntry.Config.EnableMod)
        {
            return;
        }

        DistributionSession.DrawPreview(b, __instance);
    }
}

/// <summary>
/// InventoryPage 没有 override update；GameMenu 每帧转发到当前页，因此在这里驱动分配会话。
/// </summary>
[HarmonyPatch(typeof(GameMenu), nameof(GameMenu.update))]
internal static class GameMenuUpdatePatch
{
    [HarmonyPostfix]
    private static void Postfix(GameMenu __instance)
    {
        if (!ModEntry.Config.EnableMod)
        {
            return;
        }

        if (__instance.GetCurrentPage() is InventoryPage page)
        {
            CollectSession.Tick(page);
            DistributionSession.Tick(page);
        }
        else
        {
            // 换到其他标签页：未提交的预览必须取消。
            CollectSession.Cancel();
            DistributionSession.Cancel();
        }
    }
}
