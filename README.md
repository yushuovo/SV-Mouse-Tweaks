# SV Mouse Tweaks · 星露谷鼠标手势

Mouse gestures for chests and inventory in Stardew Valley on Windows PC. Inspired by Minecraft Mouse Tweaks. Version 1.0.0 by ayushua.

## Controls

| In a chest menu, from either side | Action |
| --- | --- |
| Left-drag | Transfer stacks matching the first item |
| Shift + left-drag | Transfer every item swept over, including tools |
| Ctrl + Shift + left-click | Transfer all stack-compatible matches |
| Scroll down / up | Send / retrieve items. Default amounts are 1, 10 with Shift, or 100 with Ctrl + Shift |

Open the inventory with E and pick up a stack. Left-drag to divide it evenly, right-drag to place one per slot, or double-click to collect matching items into one stack.

Click the tray beside a chest to leave a reserve when withdrawing. The default reserve is off. When enabled, it keeps one item across matching stacks in the chest. Global and per-chest switches, reserve amounts and nonstackable categories are configurable. Generic Mod Config Menu is optional. Includes English, Simplified Chinese, Korean, Japanese and French. Android is unsupported.

## Build

Install .NET SDK 6.0.428 and SMAPI. Copy local.props.example to local.props and set GamePath to your Stardew Valley installation directory. Run the following command from the repository root.

~~~powershell
dotnet build SVMouseTweaks.csproj -c Release
~~~

## 中文说明

箱子两侧均支持扫过与滚轮转移，独立背包支持拖动均分和双击聚拢。预留默认关闭，点击箱子旁的托盘可开启。预留按整箱同类总量计算，可在 GMCM 中修改数量、适用物品和快捷键。

源码公开供查看，版权与使用权限见 [NOTICE.md](NOTICE.md)。
