using System.Collections.Concurrent;
using System.Xml.Linq;
using System.Xml.Serialization;
using StardewValley;
using StardewValley.Objects;
using StardewValley.Objects.Trinkets;
using StardewValley.Tools;
using SVMouseTweaks.Configuration;

namespace SVMouseTweaks.Reserve;

public enum ReserveItemCategory
{
    Clothing, Hats, Boots, Rings, Trinkets, Furniture, Tools, Weapons, Other,
}

/// <summary>运行时预留范围与保守同款比较；不会修改物品、堆叠规则或其他Mod配置。</summary>
public static class ReserveItemRules
{
    private static readonly ConcurrentDictionary<Type, Lazy<XmlSerializer>> Serializers = new();

    public static bool IsStackable(Item item)
    {
        return item.maximumStackSize() > 1 && item.canStackWith(item);
    }

    public static ReserveItemCategory CategoryOf(Item item) => item switch
    {
        Clothing => ReserveItemCategory.Clothing,
        Hat => ReserveItemCategory.Hats,
        Boots => ReserveItemCategory.Boots,
        Ring => ReserveItemCategory.Rings,
        Trinket => ReserveItemCategory.Trinkets,
        Furniture => ReserveItemCategory.Furniture,
        MeleeWeapon or Slingshot => ReserveItemCategory.Weapons,
        Tool => ReserveItemCategory.Tools,
        _ => ReserveItemCategory.Other,
    };

    public static bool AppliesTo(Item item, bool onlyStackable, ReserveCategories? categories)
    {
        if (IsStackable(item)) return true;
        if (onlyStackable || categories is null) return false;
        return CategoryOf(item) switch
        {
            ReserveItemCategory.Clothing => categories.Clothing,
            ReserveItemCategory.Hats => categories.Hats,
            ReserveItemCategory.Boots => categories.Boots,
            ReserveItemCategory.Rings => categories.Rings,
            ReserveItemCategory.Trinkets => categories.Trinkets,
            ReserveItemCategory.Furniture => categories.Furniture,
            ReserveItemCategory.Tools => categories.Tools,
            ReserveItemCategory.Weapons => categories.Weapons,
            _ => categories.Other,
        };
    }

    /// <summary>
    /// 仅为一次统计拍摄保存状态，不能跨帧缓存可变物品的签名。
    /// 无法序列化的扩展物品只认自身，不按名称/ID猜测不同实例相等。
    /// </summary>
    public static Func<Item, bool> CreateReserveMatcher(Item sample)
    {
        if (IsStackable(sample))
            return candidate => ReferenceEquals(sample, candidate) || sample.canStackWith(candidate);

        Type type = sample.GetType();
        string id = sample.QualifiedItemId;
        XElement? signature = SaveSignature(sample);
        return candidate => ReferenceEquals(sample, candidate)
            || (candidate.GetType() == type && candidate.QualifiedItemId == id
                && !IsStackable(candidate) && signature is not null
                && XNode.DeepEquals(signature, SaveSignature(candidate)));
    }

    private static XElement? SaveSignature(Item item)
    {
        try
        {
            XmlSerializer serializer = Serializers.GetOrAdd(item.GetType(),
                type => new Lazy<XmlSerializer>(() => new XmlSerializer(type))).Value;
            using var writer = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
            lock (serializer) serializer.Serialize(writer, item);
            XElement root = XElement.Parse(writer.ToString());

            // 排除数量、背包历史与工具动画状态；这些不改变物品用途。
            foreach (XElement field in root.Elements().Where(element =>
                         element.Name.LocalName is "stack" or "hasBeenInInventory"
                         || (item is Tool && (element.Name.LocalName is "swingTicker" or "currentParentTileIndex"))).ToList())
                field.Remove();

            // modData/饰品元数据是字典，插入顺序不应制造不同分组。
            foreach (XElement dictionary in root.Descendants().Where(element =>
                         element.Name.LocalName is "modData" or "trinketMetadata").ToList())
            {
                List<XElement> entries = dictionary.Elements().OrderBy(entry =>
                    entry.ToString(SaveOptions.DisableFormatting), StringComparer.Ordinal).ToList();
                dictionary.ReplaceNodes(entries);
            }
            return root;
        }
        catch (Exception error) when (error is InvalidOperationException or NotSupportedException
                                      or ArgumentException or System.Xml.XmlException)
        {
            return null;
        }
    }
}
