using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using StardewValley;
using SVMouseTweaks.Configuration;
using SVMouseTweaks.Input;

namespace SVMouseTweaks.Integrations;

internal static class GmcmIntegration
{
    private const string ApiUniqueId = "spacechase0.GenericModConfigMenu";

    public static void TryRegister(ModEntry mod)
    {
        IGenericModConfigMenuApi? api = mod.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>(ApiUniqueId);
        if (api is null)
        {
            mod.Monitor.Log("未检测到 Generic Mod Config Menu，使用 config.json。", LogLevel.Trace);
            return;
        }

        IManifest manifest = mod.ModManifest;
        ITranslationHelper translation = mod.Helper.Translation;

        string T(string key) => translation.Get(key);

        void Save()
        {
            ModConfig.Normalize(ModEntry.Config, mod.Monitor);
            ModEntry.SaveConfig();
            InputCoordinator.EndAllSessions();
            ShowDuplicateWarnings(mod);
        }

        api.Register(
            manifest,
            reset: () =>
            {
                ModEntry.Config = new ModConfig();
                ModEntry.SaveConfig();
                InputCoordinator.EndAllSessions();
            },
            save: Save);

        api.AddSectionTitle(manifest, () => T("gmcm.section.general"));
        api.AddBoolOption(
            manifest,
            () => ModEntry.Config.EnableMod,
            value => ModEntry.Config.EnableMod = value,
            () => T("gmcm.enableMod"),
            () => T("gmcm.enableMod.tooltip"),
            "EnableMod");

        api.AddSectionTitle(manifest, () => T("gmcm.section.chest"));
        AddModifier(api, manifest, T, "gmcm.chest.sameType", "ChestSameTypeModifiers",
            () => ModEntry.Config.EnableChestSameTypeSweep, v => ModEntry.Config.EnableChestSameTypeSweep = v,
            () => ModEntry.Config.ChestSameTypeModifiers, v => ModEntry.Config.ChestSameTypeModifiers = v);
        AddModifier(api, manifest, T, "gmcm.chest.allItems", "ChestAllItemsModifiers",
            () => ModEntry.Config.EnableChestAllItemsSweep, v => ModEntry.Config.EnableChestAllItemsSweep = v,
            () => ModEntry.Config.ChestAllItemsModifiers, v => ModEntry.Config.ChestAllItemsModifiers = v);
        AddModifier(api, manifest, T, "gmcm.chest.takeAll", "ChestTakeAllModifiers",
            () => ModEntry.Config.EnableChestTakeAllOfType, v => ModEntry.Config.EnableChestTakeAllOfType = v,
            () => ModEntry.Config.ChestTakeAllModifiers, v => ModEntry.Config.ChestTakeAllModifiers = v);

        api.AddSectionTitle(manifest, () => T("gmcm.section.wheel"));
        api.AddBoolOption(
            manifest,
            () => ModEntry.Config.EnableWheel,
            value => ModEntry.Config.EnableWheel = value,
            () => T("gmcm.wheel.enable"),
            () => T("gmcm.wheel.enable.tooltip"),
            "EnableWheel");

        AddWheelTier(api, manifest, T, "gmcm.wheel.small", "WheelSmallAmount", "WheelSmallModifiers",
            () => ModEntry.Config.WheelSmallAmount, v => ModEntry.Config.WheelSmallAmount = v,
            () => ModEntry.Config.WheelSmallModifiers, v => ModEntry.Config.WheelSmallModifiers = v);
        AddWheelTier(api, manifest, T, "gmcm.wheel.medium", "WheelMediumAmount", "WheelMediumModifiers",
            () => ModEntry.Config.WheelMediumAmount, v => ModEntry.Config.WheelMediumAmount = v,
            () => ModEntry.Config.WheelMediumModifiers, v => ModEntry.Config.WheelMediumModifiers = v);
        AddWheelTier(api, manifest, T, "gmcm.wheel.large", "WheelLargeAmount", "WheelLargeModifiers",
            () => ModEntry.Config.WheelLargeAmount, v => ModEntry.Config.WheelLargeAmount = v,
            () => ModEntry.Config.WheelLargeModifiers, v => ModEntry.Config.WheelLargeModifiers = v);

        api.AddBoolOption(
            manifest,
            () => ModEntry.Config.WheelReverse,
            value => ModEntry.Config.WheelReverse = value,
            () => T("gmcm.wheel.reverse"),
            () => T("gmcm.wheel.reverse.tooltip"),
            "WheelReverse");

        api.AddBoolOption(
            manifest,
            () => ModEntry.Config.WheelOppositeInventorySide,
            value => ModEntry.Config.WheelOppositeInventorySide = value,
            () => T("gmcm.wheel.oppositeInventory"),
            () => T("gmcm.wheel.oppositeInventory.tooltip"),
            "WheelOppositeInventorySide");

        api.AddSectionTitle(manifest, () => T("gmcm.section.inventory"));
        AddModifier(api, manifest, T, "gmcm.inventory.divide", "InventoryDivideModifiers",
            () => ModEntry.Config.EnableInventoryDivide, v => ModEntry.Config.EnableInventoryDivide = v,
            () => ModEntry.Config.InventoryDivideModifiers, v => ModEntry.Config.InventoryDivideModifiers = v);
        AddModifier(api, manifest, T, "gmcm.inventory.distribute", "InventoryDistributeModifiers",
            () => ModEntry.Config.EnableInventoryDistribute, v => ModEntry.Config.EnableInventoryDistribute = v,
            () => ModEntry.Config.InventoryDistributeModifiers, v => ModEntry.Config.InventoryDistributeModifiers = v);
        api.AddBoolOption(
            manifest,
            () => ModEntry.Config.EnableInventoryCollect,
            value => ModEntry.Config.EnableInventoryCollect = value,
            () => T("gmcm.inventory.collect.enabled"),
            () => T("gmcm.inventory.collect.enabled.tooltip"),
            "EnableInventoryCollect");

        api.AddSectionTitle(manifest, () => T("gmcm.section.reserve"));
        api.AddTextOption(
            manifest,
            () => ModEntry.Config.ReserveMode.ToString(),
            value => ModEntry.Config.ReserveMode =
                Enum.TryParse(value, ignoreCase: true, out ReserveMode mode) ? mode : ReserveMode.Global,
            () => T("gmcm.reserve.mode"),
            () => T("gmcm.reserve.mode.tooltip"),
            new[] { nameof(ReserveMode.Global), nameof(ReserveMode.PerChest) },
            value => T(value.Equals(nameof(ReserveMode.PerChest), StringComparison.OrdinalIgnoreCase)
                ? "reserve.mode.perChest"
                : "reserve.mode.global"),
            "ReserveMode");

        api.AddBoolOption(
            manifest,
            () => ModEntry.Config.ReserveGlobalEnabled,
            value => ModEntry.Config.ReserveGlobalEnabled = value,
            () => T("gmcm.reserve.globalEnabled"),
            () => T("gmcm.reserve.globalEnabled.tooltip"),
            "ReserveGlobalEnabled");

        api.AddNumberOption(
            manifest,
            () => ModEntry.Config.ReserveAmount,
            value => ModEntry.Config.ReserveAmount = value,
            () => T("gmcm.reserve.amount"),
            () => T("gmcm.reserve.amount.tooltip"),
            min: 1,
            max: 99,
            interval: 1,
            fieldId: "ReserveAmount");

        api.AddBoolOption(
            manifest,
            () => ModEntry.Config.ReserveAffectsLeftClick,
            value => ModEntry.Config.ReserveAffectsLeftClick = value,
            () => T("gmcm.reserve.leftClick"),
            () => T("gmcm.reserve.leftClick.tooltip"),
            "ReserveAffectsLeftClick");

        api.AddBoolOption(
            manifest,
            () => ModEntry.Config.ReserveAffectsRightClick,
            value => ModEntry.Config.ReserveAffectsRightClick = value,
            () => T("gmcm.reserve.rightClick"),
            () => T("gmcm.reserve.rightClick.tooltip"),
            "ReserveAffectsRightClick");

        api.AddBoolOption(
            manifest,
            () => ModEntry.Config.ReserveAffectsShiftRightClick,
            value => ModEntry.Config.ReserveAffectsShiftRightClick = value,
            () => T("gmcm.reserve.shiftRightClick"),
            () => T("gmcm.reserve.shiftRightClick.tooltip"),
            "ReserveAffectsShiftRightClick");

        api.AddBoolOption(manifest,
            () => ModEntry.Config.ReserveOnlyStackable,
            value => ModEntry.Config.ReserveOnlyStackable = value,
            () => T("gmcm.reserve.onlyStackable"),
            () => T("gmcm.reserve.onlyStackable.tooltip"), "ReserveOnlyStackable");
        api.AddSectionTitle(manifest, () => T("gmcm.reserve.categories"),
            () => T("gmcm.reserve.categories.tooltip"));
        AddReserveCategory("clothing", () => ModEntry.Config.ReserveCategories.Clothing,
            value => ModEntry.Config.ReserveCategories.Clothing = value);
        AddReserveCategory("hats", () => ModEntry.Config.ReserveCategories.Hats,
            value => ModEntry.Config.ReserveCategories.Hats = value);
        AddReserveCategory("boots", () => ModEntry.Config.ReserveCategories.Boots,
            value => ModEntry.Config.ReserveCategories.Boots = value);
        AddReserveCategory("rings", () => ModEntry.Config.ReserveCategories.Rings,
            value => ModEntry.Config.ReserveCategories.Rings = value);
        AddReserveCategory("trinkets", () => ModEntry.Config.ReserveCategories.Trinkets,
            value => ModEntry.Config.ReserveCategories.Trinkets = value);
        AddReserveCategory("furniture", () => ModEntry.Config.ReserveCategories.Furniture,
            value => ModEntry.Config.ReserveCategories.Furniture = value);
        AddReserveCategory("tools", () => ModEntry.Config.ReserveCategories.Tools,
            value => ModEntry.Config.ReserveCategories.Tools = value);
        AddReserveCategory("weapons", () => ModEntry.Config.ReserveCategories.Weapons,
            value => ModEntry.Config.ReserveCategories.Weapons = value);
        AddReserveCategory("other", () => ModEntry.Config.ReserveCategories.Other,
            value => ModEntry.Config.ReserveCategories.Other = value);

        void AddReserveCategory(string key, Func<bool> get, Action<bool> set) =>
            api.AddBoolOption(manifest, get, set,
                () => T($"gmcm.reserve.category.{key}"),
                () => T("gmcm.reserve.categories.tooltip"), $"ReserveCategories.{key}");
    }

    private static void AddModifier(
        IGenericModConfigMenuApi api,
        IManifest manifest,
        Func<string, string> translate,
        string keyPrefix,
        string fieldId,
        Func<bool> getEnabled,
        Action<bool> setEnabled,
        Func<string> getModifiers,
        Action<string> setModifiers)
    {
        api.AddBoolOption(
            manifest,
            getEnabled,
            setEnabled,
            () => translate($"{keyPrefix}.enabled"),
            () => translate($"{keyPrefix}.enabled.tooltip"),
            $"{fieldId}.enabled");

        api.AddKeybindList(
            manifest,
            () => ModifierBinding.ParseOrEmpty(getModifiers()).ToKeybindList(),
            value => setModifiers(ModifierBinding.FromKeybindList(value)),
            () => translate($"{keyPrefix}.modifiers"),
            () => translate("gmcm.modifiers.tooltip"),
            fieldId: fieldId);
    }

    private static void AddWheelTier(
        IGenericModConfigMenuApi api,
        IManifest manifest,
        Func<string, string> translate,
        string keyPrefix,
        string amountFieldId,
        string modifiersFieldId,
        Func<int> getAmount,
        Action<int> setAmount,
        Func<string> getModifiers,
        Action<string> setModifiers)
    {
        api.AddNumberOption(
            manifest,
            getAmount,
            setAmount,
            () => translate($"{keyPrefix}.amount"),
            () => translate($"{keyPrefix}.amount.tooltip"),
            min: 1,
            max: 999,
            interval: 1,
            fieldId: amountFieldId);

        api.AddKeybindList(
            manifest,
            () => ModifierBinding.ParseOrEmpty(getModifiers()).ToKeybindList(),
            value => setModifiers(ModifierBinding.FromKeybindList(value)),
            () => translate($"{keyPrefix}.modifiers"),
            () => translate("gmcm.modifiers.tooltip"),
            fieldId: modifiersFieldId);
    }

    private static void ShowDuplicateWarnings(ModEntry mod)
    {
        if (InputCoordinator.HasDuplicateChestBindings(ModEntry.Config, out string chestActions))
        {
            Notify(mod, mod.Helper.Translation.Get("gmcm.duplicate.chest", new { actions = chestActions }));
        }

        if (InputCoordinator.HasDuplicateWheelBindings(ModEntry.Config, out string wheelActions))
        {
            Notify(mod, mod.Helper.Translation.Get("gmcm.duplicate.wheel", new { actions = wheelActions }));
        }
    }

    private static void Notify(ModEntry mod, string message)
    {
        mod.Monitor.Log(message, LogLevel.Warn);
        if (Context.IsWorldReady)
        {
            Game1.addHUDMessage(new HUDMessage(message, HUDMessage.error_type));
        }
    }
}
