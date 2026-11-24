using HarmonyLib;
using StardewValley.Menus;
using SVMouseTweaks.Transfers;

namespace SVMouseTweaks.Patches;

/// <summary>
/// 滚轮在基类 IClickableMenu 上分发；ItemGrabMenu 未 override，因此在基类拦截即可。
/// </summary>
[HarmonyPatch(typeof(IClickableMenu), nameof(IClickableMenu.receiveScrollWheelAction))]
internal static class MenuBasePatches
{
    [HarmonyPrefix]
    private static bool Prefix(IClickableMenu __instance, int direction)
    {
        if (!ModEntry.Config.EnableMod)
        {
            return true;
        }

        return !WheelController.TryHandle(__instance, direction);
    }
}
