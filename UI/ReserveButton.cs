using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;
using SVMouseTweaks.Configuration;
using SVMouseTweaks.Input;
using SVMouseTweaks.Reserve;

namespace SVMouseTweaks.UI;

/// <summary>
/// 箱子菜单右侧的“预留”托盘按钮。16x16 素材按 4 倍绘制，开状态动态画 N。
/// </summary>
public sealed class ReserveButton
{
    private static readonly ConditionalWeakTable<ItemGrabMenu, ReserveButton> Buttons = new();

    private readonly ClickableTextureComponent _component;

    private ReserveButton(ClickableTextureComponent component)
    {
        _component = component;
    }

    public static ReserveButton? Ensure(ItemGrabMenu menu)
    {
        if (menu.shippingBin || ModEntry.ReserveOnTexture is null || ModEntry.ReserveOffTexture is null)
        {
            return null;
        }

        if (!Buttons.TryGetValue(menu, out ReserveButton? button))
        {
            var bounds = new Rectangle(0, 0, 64, 64);
            var component = new ClickableTextureComponent(
                "SVMouseTweaks/Reserve",
                bounds,
                string.Empty,
                string.Empty,
                ModEntry.ReserveOffTexture,
                new Rectangle(0, 0, 16, 16),
                4f,
                drawShadow: false);
            button = new ReserveButton(component);
            Buttons.Add(menu, button);
        }

        button.Relayout(menu);
        return button;
    }

    public static bool TryClick(ItemGrabMenu menu, Chest chest, int x, int y)
    {
        ReserveButton? button = Ensure(menu);
        if (button is null || !button._component.containsPoint(x, y))
        {
            return false;
        }

        ModConfig config = ModEntry.Config;
        if (config.ReserveMode == ReserveMode.Global)
        {
            config.ReserveGlobalEnabled = !config.ReserveGlobalEnabled;
            ModEntry.SaveConfig();
        }
        else
        {
            ReserveStateStore.SetPerChest(chest, !ReserveStateStore.IsPerChestEnabled(chest));
        }

        InputCoordinator.EndAllSessions();
        Game1.playSound("smallSelect");
        return true;
    }

    public void Draw(SpriteBatch batch, ItemGrabMenu menu, Chest chest)
    {
        Relayout(menu);
        bool enabled = ReserveStateStore.IsReserveEnabled(chest);
        Texture2D texture = (enabled ? ModEntry.ReserveOnTexture : ModEntry.ReserveOffTexture)!;

        int mouseX = Game1.getMouseX();
        int mouseY = Game1.getMouseY();
        _component.tryHover(mouseX, mouseY);

        bool pressed = InputCoordinator.IsKeyDown(SButton.MouseLeft) && _component.containsPoint(mouseX, mouseY);
        var center = new Vector2(
            _component.bounds.Center.X,
            _component.bounds.Center.Y + (pressed ? 2 : 0));

        // 使用已确认配色的完整托盘素材。mouseCursors(162,440)是整理图案，
        // 不是空白背板，不能叠在透明前景下冒充原版按钮背景。
        batch.Draw(
            texture,
            center,
            new Rectangle(0, 0, 16, 16),
            Color.White,
            0f,
            new Vector2(8f, 8f),
            _component.scale,
            SpriteEffects.None,
            0.91f);

        if (enabled)
        {
            int amount = Math.Clamp(ModEntry.Config.ReserveAmount, 1, 99);
            float scale = 2f;
            float width = Utility.getWidthOfTinyDigitString(amount, scale);
            var position = new Vector2(
                _component.bounds.Right - width - 4f,
                _component.bounds.Y + 48f);

            Utility.drawTinyDigits(amount, batch, position + new Vector2(1f, 1f), scale, 0.92f, Color.Black * 0.75f);
            Utility.drawTinyDigits(amount, batch, position, scale, 0.93f, Color.White);
        }

        if (_component.containsPoint(mouseX, mouseY))
        {
            string mode = ModEntry.Config.ReserveMode == ReserveMode.Global ? "global" : "perChest";
            string state = enabled ? "on" : "off";
            string tooltip = ModEntry.Instance.Helper.Translation.Get($"reserve.tooltip.{mode}.{state}");
            IClickableMenu.drawHoverText(batch, tooltip, Game1.smallFont);
        }
    }

    private void Relayout(ItemGrabMenu menu)
    {
        int count = CountVanillaButtons(menu);
        int spacing = count >= 4 ? 72 : 80;
        int x = menu.ItemsToGrabMenu.xPositionOnScreen + menu.ItemsToGrabMenu.width + IClickableMenu.borderWidth * 2;
        // 与原版 RepositionSideButtons 一致：纵向基准使用菜单自身高度，而不是箱子网格高度。
        int y = menu.ItemsToGrabMenu.yPositionOnScreen + menu.height / 3 - 64 - spacing * count;
        _component.bounds = new Rectangle(x, y, 64, 64);
    }

    private static int CountVanillaButtons(ItemGrabMenu menu)
    {
        int count = 0;
        count += menu.organizeButton is not null ? 1 : 0;
        count += menu.fillStacksButton is not null ? 1 : 0;
        count += menu.colorPickerToggleButton is not null ? 1 : 0;
        count += menu.specialButton is not null ? 1 : 0;
        count += menu.junimoNoteIcon is not null ? 1 : 0;
        return count;
    }
}
