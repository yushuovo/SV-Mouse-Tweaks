using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley.Menus;
using StardewValley.Objects;
using SVMouseTweaks.Configuration;
using SVMouseTweaks.Input;
using SVMouseTweaks.Integrations;
using SVMouseTweaks.Inventory;
using SVMouseTweaks.Transfers;

namespace SVMouseTweaks;

public sealed class ModEntry : Mod
{
    public static ModEntry Instance { get; private set; } = null!;

    public static ModConfig Config = null!;

    public static Texture2D? ReserveOnTexture { get; private set; }

    public static Texture2D? ReserveOffTexture { get; private set; }

    public override void Entry(IModHelper helper)
    {
        Instance = this;
        Config = helper.ReadConfig<ModConfig>() ?? new ModConfig();

        if (ModConfig.Normalize(Config, Monitor))
        {
            Monitor.Log("配置中存在非法值，已自动修正并写回。", LogLevel.Warn);
            helper.WriteConfig(Config);
        }

        try
        {
            ReserveOnTexture = helper.ModContent.Load<Texture2D>("assets/reserve-on.png");
            ReserveOffTexture = helper.ModContent.Load<Texture2D>("assets/reserve-off.png");
        }
        catch (Exception ex)
        {
            Monitor.Log($"预留按钮素材加载失败：{ex.Message}", LogLevel.Error);
        }

        try
        {
            new Harmony(ModManifest.UniqueID).PatchAll();
        }
        catch (Exception ex)
        {
            Monitor.Log($"Harmony 补丁应用失败：{ex}", LogLevel.Error);
        }

        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += (_, _) => InputCoordinator.EndAllSessions();
        helper.Events.GameLoop.ReturnedToTitle += (_, _) => InputCoordinator.EndAllSessions();
        helper.Events.Display.MenuChanged += OnMenuChanged;
    }

    public static void SaveConfig()
    {
        Instance.Helper.WriteConfig(Config);
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        GmcmIntegration.TryRegister(this);
    }

    private void OnMenuChanged(object? sender, MenuChangedEventArgs e)
    {
        ChestGestureSession.OnMenuChanged(e.NewMenu);
        WheelController.OnMenuChanged(e.NewMenu);
        DistributionSession.OnMenuChanged(e.NewMenu);
        CollectSession.OnMenuChanged(e.NewMenu);
    }
}
