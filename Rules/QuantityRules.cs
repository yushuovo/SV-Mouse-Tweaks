namespace SVMouseTweaks.Rules;

/// <summary>
/// 数量规则：一次从某个来源取出的数量，同时受请求量、来源剩余可支配量
/// （同类总量减去预留量）和目标可容纳量限制。
/// </summary>
public static class QuantityRules
{
    public const int MaxReserve = 99;

    /// <param name="requested">本次希望取出的数量（通常是整堆）。</param>
    /// <param name="sourceTotal">来源侧该可堆叠类型的总量，跨所有堆计算。</param>
    /// <param name="reserve">预留量；0 表示不预留。</param>
    /// <param name="capacity">目标侧还能接收的数量。</param>
    public static int Allowed(int requested, long sourceTotal, int reserve, int capacity)
    {
        if (requested <= 0 || capacity <= 0 || sourceTotal <= 0)
        {
            return 0;
        }

        long effectiveReserve = Math.Clamp(reserve, 0, MaxReserve);
        long available = sourceTotal - effectiveReserve;
        if (available <= 0)
        {
            return 0;
        }

        long allowed = Math.Min(requested, Math.Min(available, capacity));
        if (allowed <= 0)
        {
            return 0;
        }

        return (int)Math.Min(allowed, int.MaxValue);
    }
}
