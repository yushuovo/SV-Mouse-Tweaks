using System.Collections.Concurrent;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;

namespace SVMouseTweaks.Configuration;

/// <summary>
/// 辅助键绑定。内部保存 SMAPI 的 <see cref="KeybindList"/>，因此 GMCM 的原生按键录入
/// 与配置文件里的字符串是同一份语义；旧式 Shift/Ctrl+Shift、A|B 文本会在读取时迁移。
/// </summary>
public sealed class ModifierBinding
{
    public static readonly ModifierBinding Empty = new(new KeybindList());

    private static readonly ConcurrentDictionary<string, ModifierBinding> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly KeybindList _keybindList;

    private ModifierBinding(KeybindList keybindList)
    {
        _keybindList = keybindList;
        NormalizedKey = BuildNormalizedKey(keybindList);
    }

    public bool IsEmpty => !_keybindList.IsBound;

    /// <summary>最具体组合中的按键数；空绑定为 0。</summary>
    public int Specificity
    {
        get
        {
            int best = 0;
            foreach (Keybind keybind in _keybindList.Keybinds)
            {
                best = Math.Max(best, keybind.Buttons.Length);
            }

            return best;
        }
    }

    /// <summary>用于重复绑定检测的规范化键（与顺序无关）。</summary>
    public string NormalizedKey { get; }

    public KeybindList ToKeybindList()
    {
        return _keybindList;
    }

    public string ToConfigString()
    {
        return _keybindList.IsBound ? _keybindList.ToString() : string.Empty;
    }

    public static string FromKeybindList(KeybindList? keybindList)
    {
        return keybindList is null || !keybindList.IsBound
            ? string.Empty
            : new ModifierBinding(keybindList).ToConfigString();
    }

    public static ModifierBinding ParseOrEmpty(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Empty;
        }

        return Cache.GetOrAdd(text, static value => TryParse(value, out ModifierBinding binding, out _)
            ? binding
            : Empty);
    }

    public static bool TryParse(string? text, out ModifierBinding binding, out string? error)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            binding = Empty;
            error = null;
            return true;
        }

        // 优先按 SMAPI 的 KeybindList 文本解析（GMCM 写入的规范格式）。
        if (KeybindList.TryParse(text, out KeybindList? parsed, out _) && parsed is not null)
        {
            binding = parsed.IsBound ? new ModifierBinding(parsed) : Empty;
            error = null;
            return true;
        }

        // 兼容旧配置：Shift/Ctrl/Alt、+ 分组、| 组内任一。
        if (TryParseLegacy(text, out List<Keybind> keybinds, out string? legacyError))
        {
            binding = keybinds.Count == 0 ? Empty : new ModifierBinding(new KeybindList(keybinds.ToArray()));
            error = null;
            return true;
        }

        binding = Empty;
        error = legacyError;
        return false;
    }

    /// <summary>任一 keybind 的所有按键都按下即满足；空绑定恒满足。</summary>
    public bool IsSatisfied(Func<SButton, bool> isDown)
    {
        if (!_keybindList.IsBound)
        {
            return true;
        }

        foreach (Keybind keybind in _keybindList.Keybinds)
        {
            bool all = true;
            foreach (SButton button in keybind.Buttons)
            {
                if (!isDown(button))
                {
                    all = false;
                    break;
                }
            }

            if (all)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryParseLegacy(string text, out List<Keybind> keybinds, out string? error)
    {
        keybinds = new List<Keybind>();
        var groups = new List<List<SButton>>();

        foreach (string raw in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var group = new List<SButton>();
            foreach (string part in raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                SButton[]? resolved = Resolve(part);
                if (resolved is null)
                {
                    error = $"无法识别的辅助键“{part}”。请使用 Shift/Ctrl/Alt 或 SButton 名称，例如 LeftShift。";
                    return false;
                }

                group.AddRange(resolved);
            }

            if (group.Count == 0)
            {
                error = $"辅助键组“{raw}”为空。";
                return false;
            }

            groups.Add(group.Distinct().ToList());
        }

        if (groups.Count == 0)
        {
            error = null;
            return true;
        }

        // 每个 + 组取一个键，做笛卡尔积；Shift 这种别名会展开成左右两个 keybind。
        var combinations = new List<SButton[]> { Array.Empty<SButton>() };
        foreach (List<SButton> group in groups)
        {
            var next = new List<SButton[]>();
            foreach (SButton[] existing in combinations)
            {
                foreach (SButton key in group)
                {
                    var combination = new SButton[existing.Length + 1];
                    Array.Copy(existing, combination, existing.Length);
                    combination[^1] = key;
                    next.Add(combination);
                }
            }

            combinations = next;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (SButton[] combination in combinations)
        {
            string key = string.Join("+", combination.Select(button => button.ToString()).OrderBy(name => name, StringComparer.Ordinal));
            if (seen.Add(key))
            {
                keybinds.Add(new Keybind(combination));
            }
        }

        error = null;
        return true;
    }

    private static SButton[]? Resolve(string token)
    {
        string key = token.Trim();
        if (key.Length == 0)
        {
            return null;
        }

        if (key.Equals("shift", StringComparison.OrdinalIgnoreCase))
        {
            return new[] { SButton.LeftShift, SButton.RightShift };
        }

        if (key.Equals("ctrl", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("control", StringComparison.OrdinalIgnoreCase))
        {
            return new[] { SButton.LeftControl, SButton.RightControl };
        }

        if (key.Equals("alt", StringComparison.OrdinalIgnoreCase))
        {
            return new[] { SButton.LeftAlt, SButton.RightAlt };
        }

        if (key.Equals("win", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("windows", StringComparison.OrdinalIgnoreCase))
        {
            return new[] { SButton.LeftWindows, SButton.RightWindows };
        }

        if (key.StartsWith("Mouse", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!Enum.TryParse(key, ignoreCase: true, out SButton button) ||
            !Enum.IsDefined(typeof(SButton), button) ||
            button == SButton.None)
        {
            return null;
        }

        return new[] { button };
    }

    private static string BuildNormalizedKey(KeybindList keybindList)
    {
        if (!keybindList.IsBound)
        {
            return string.Empty;
        }

        return string.Join(
            ", ",
            keybindList.Keybinds
                .Select(keybind => keybind.ToString())
                .OrderBy(text => text, StringComparer.Ordinal));
    }
}
