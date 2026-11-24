using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;
using StardewValley.Menus;
using StardewValley.Objects;
using SVMouseTweaks.Transfers;
using SVMouseTweaks.UI;

namespace SVMouseTweaks.Patches;

[HarmonyPatch(typeof(ItemGrabMenu))]
internal static class ItemGrabMenuPatches
{
    [HarmonyPatch(nameof(ItemGrabMenu.receiveLeftClick))]
    [HarmonyPrefix]
    private static bool ReceiveLeftClickPrefix(ItemGrabMenu __instance, int x, int y)
    {
        if (ChestGestureSession.IsBypassing)
        {
            // 普通左键单击的“暂存后松开”路径：放行一次真正的原版点击。
            return true;
        }

        if (!ModEntry.Config.EnableMod)
        {
            return true;
        }

        if (__instance.shippingBin ||
            __instance.source != ItemGrabMenu.source_chest ||
            __instance.sourceItem is not Chest chest)
        {
            return true;
        }

        if (!ChestSupport.IsSupported(chest))
        {
            return true;
        }

        if (ReserveButton.TryClick(__instance, chest, x, y))
        {
            return false;
        }

        if (ChestGestureSession.TryHandleClick(__instance, x, y))
        {
            return false;
        }

        return true;
    }

    [HarmonyPatch(nameof(ItemGrabMenu.receiveRightClick))]
    [HarmonyPrefix]
    private static bool ReceiveRightClickPrefix(ItemGrabMenu __instance, int x, int y)
    {
        if (!ModEntry.Config.EnableMod)
        {
            return true;
        }

        if (__instance.shippingBin ||
            __instance.source != ItemGrabMenu.source_chest ||
            __instance.sourceItem is not Chest chest)
        {
            return true;
        }

        if (!ChestSupport.IsSupported(chest))
        {
            return true;
        }

        if (ChestGestureSession.TryHandleReservedRightClick(__instance, x, y))
        {
            return false;
        }

        // 拖动会话期间不新增箱子右键扫过；玩家背包右键交还原版。
        return !(ChestGestureSession.IsActive
            && __instance.ItemsToGrabMenu.getInventoryPositionOfClick(x, y) >= 0);
    }

    [HarmonyPatch(nameof(ItemGrabMenu.update))]
    [HarmonyPostfix]
    private static void UpdatePostfix(ItemGrabMenu __instance)
    {
        if (!ModEntry.Config.EnableMod)
        {
            return;
        }

        ChestGestureSession.Tick(__instance);
        WheelController.Tick(__instance);
    }

    [HarmonyPatch(nameof(ItemGrabMenu.draw))]
    [HarmonyPostfix]
    private static void DrawPostfix(ItemGrabMenu __instance, SpriteBatch b)
    {
        if (!ModEntry.Config.EnableMod)
        {
            return;
        }

        if (__instance.source != ItemGrabMenu.source_chest || __instance.sourceItem is not Chest chest)
        {
            return;
        }

        if (!ChestSupport.IsSupported(chest))
        {
            return;
        }

        ReserveButton.Ensure(__instance)?.Draw(b, __instance, chest);
    }
}
