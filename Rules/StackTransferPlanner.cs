namespace SVMouseTweaks.Rules;

public readonly record struct StackTarget(int Index, int Stack);

public readonly record struct MergePlan(int Index, int Add);

public readonly record struct NewStackPlan(int SlotIndex, int Stack);

public sealed record TransferAllocation(
    IReadOnlyList<MergePlan> Merges,
    IReadOnlyList<NewStackPlan> NewStacks,
    int Moved,
    int Remaining);

/// <summary>
/// 把要移动的数量先合并进目标侧已有的同类堆，再放入空槽。
/// 纯计算，不接触游戏对象，便于离线检查。
/// </summary>
public static class StackTransferPlanner
{
    public static TransferAllocation Plan(
        int amount,
        int maxStackSize,
        IReadOnlyList<StackTarget> existing,
        IReadOnlyList<int> emptySlots)
    {
        int requested = Math.Max(0, amount);
        int remaining = requested;
        int stackLimit = Math.Max(1, maxStackSize);
        var merges = new List<MergePlan>();
        var newStacks = new List<NewStackPlan>();

        foreach (StackTarget target in existing)
        {
            if (remaining == 0)
            {
                break;
            }

            int space = Math.Max(0, stackLimit - Math.Max(0, target.Stack));
            if (space == 0)
            {
                continue;
            }

            int add = Math.Min(space, remaining);
            merges.Add(new MergePlan(target.Index, add));
            remaining -= add;
        }

        foreach (int slot in emptySlots)
        {
            if (remaining == 0)
            {
                break;
            }

            int stack = Math.Min(stackLimit, remaining);
            newStacks.Add(new NewStackPlan(slot, stack));
            remaining -= stack;
        }

        return new TransferAllocation(merges, newStacks, requested - remaining, remaining);
    }
}
