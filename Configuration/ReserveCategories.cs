namespace SVMouseTweaks.Configuration;

/// <summary>只用于当前不可堆叠物品；所有分类默认关闭，保留旧版本行为。</summary>
public sealed class ReserveCategories
{
    public bool Clothing { get; set; }
    public bool Hats { get; set; }
    public bool Boots { get; set; }
    public bool Rings { get; set; }
    public bool Trinkets { get; set; }
    public bool Furniture { get; set; }
    public bool Tools { get; set; }
    public bool Weapons { get; set; }
    public bool Other { get; set; }
}
