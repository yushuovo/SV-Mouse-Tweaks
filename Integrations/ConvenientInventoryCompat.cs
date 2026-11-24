using System.Reflection;
using StardewModdingAPI;
using StardewValley.Objects;

namespace SVMouseTweaks.Integrations;

/// <summary>
/// Convenient Inventory 的自动整理协调。本 Mod 直接写箱子列表，不会触发其
/// <c>Chest.addItem</c> / <c>grabItemFromInventory</c> postfix；因此在玩家→箱子
/// 的批量转移结束后，若 CI 的自动整理已开启，主动调用一次其整理逻辑。
/// 通过反射调用，CI 缺失或版本不匹配时安全跳过。
/// </summary>
internal static class ConvenientInventoryCompat
{
    private const string ModId = "gaussfire.ConvenientInventory";
    private static bool _warned;

    public static void TryOrganizeAfterDeposit(Chest chest)
    {
        if (ModEntry.Instance.Helper.ModRegistry.Get(ModId) is null)
        {
            return;
        }

        try
        {
            Assembly? assembly = AppDomain.CurrentDomain
                .GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "ConvenientInventory");
            if (assembly is null)
            {
                return;
            }

            Type? modEntry = assembly.GetType("ConvenientInventory.ModEntry");
            object? config = GetStaticMember(modEntry, "Config");
            if (config is null)
            {
                return;
            }

            object? enabled = config.GetType()
                .GetProperty("IsEnableAutoOrganizeChest", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(config);
            if (enabled is not true)
            {
                return;
            }

            Type? logic = assembly.GetType("ConvenientInventory.AutoOrganize.AutoOrganizeLogic");
            MethodInfo? method = logic?.GetMethod(
                "TryOrganizeChest",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(Chest) },
                modifiers: null);
            method?.Invoke(null, new object[] { chest });
        }
        catch (Exception ex)
        {
            if (!_warned)
            {
                _warned = true;
                ModEntry.Instance.Monitor.Log(
                    $"Convenient Inventory auto-organize coordination skipped: {ex.GetType().Name}: {ex.Message}",
                    LogLevel.Trace);
            }
        }
    }

    private static object? GetStaticMember(Type? type, string name)
    {
        if (type is null)
        {
            return null;
        }

        return type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null)
            ?? type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
    }
}
