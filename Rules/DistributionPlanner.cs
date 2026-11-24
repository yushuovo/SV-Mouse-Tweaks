namespace SVMouseTweaks.Rules;

public readonly record struct SlotCapacity(int SlotIndex, int Capacity);

public readonly record struct SlotAddition(int SlotIndex, int Add);

public sealed record DistributionPlan(
    IReadOnlyList<SlotAddition> Slots,
    int PerSlot,
    int TotalAdded,
    int Remaining);

/// <summary>
/// 左键拖动均分的纯计划：每个有效格追加 floor(持有量 / 格数)，
/// 受每格剩余容量限制，余数留在鼠标上。
/// </summary>
public static class DistributionPlanner
{
    public static DistributionPlan Plan(int heldAmount, IReadOnlyList<SlotCapacity> slots)
    {
        if (heldAmount <= 0 || slots.Count == 0)
        {
            return new DistributionPlan(Array.Empty<SlotAddition>(), 0, 0, Math.Max(0, heldAmount));
        }

        int perSlot = heldAmount / slots.Count;
        var additions = new List<SlotAddition>(slots.Count);
        int totalAdded = 0;

        foreach (SlotCapacity slot in slots)
        {
            int capacity = Math.Max(0, slot.Capacity);
            int add = Math.Min(perSlot, capacity);
            if (add <= 0)
            {
                continue;
            }

            additions.Add(new SlotAddition(slot.SlotIndex, add));
            totalAdded += add;
        }

        return new DistributionPlan(additions, perSlot, totalAdded, heldAmount - totalAdded);
    }
}
