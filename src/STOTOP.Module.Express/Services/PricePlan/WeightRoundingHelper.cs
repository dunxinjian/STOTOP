namespace STOTOP.Module.Express.Services.PricePlan;

/// <summary>
/// 重量进位算法（7种）
/// </summary>
public static class WeightRoundingHelper
{
    /// <summary>
    /// 根据进位方式对重量进行进位处理
    /// </summary>
    /// <param name="weight">原始重量</param>
    /// <param name="method">进位方式 1-7</param>
    /// <param name="truncParam">截断参数（方式4/6/7暂未使用，预留）</param>
    /// <param name="ceilParam">进位参数（方式4阈值/方式6步进值/方式7进位基数）</param>
    /// <returns>进位后的重量</returns>
    public static decimal RoundWeight(decimal weight, int method, decimal? truncParam, decimal? ceilParam)
    {
        return method switch
        {
            1 => weight,                                                    // 实际重量
            2 => Math.Round(weight, 0, MidpointRounding.AwayFromZero),     // 四舍五入
            3 => RoundHalfKg(weight),                                       // 点五进位
            4 => RoundByThreshold(weight, truncParam, ceilParam),          // 进舍（按阈值）
            5 => Math.Ceiling(weight),                                      // 向上取整
            6 => RoundBySegment(weight, truncParam, ceilParam),            // 分段进位（按步进值）
            7 => RoundByHundredth(weight, truncParam, ceilParam),          // 进舍百位
            _ => weight
        };
    }

    /// <summary>
    /// 点五进位：小数部分 ≥ 0.5 向上取整，否则向下取整
    /// </summary>
    private static decimal RoundHalfKg(decimal weight)
        => weight % 1 >= 0.5m ? Math.Ceiling(weight) : Math.Floor(weight);

    /// <summary>
    /// 进舍：小数部分 ≥ 阈值(ceilParam)则向上取整，否则向下取整。
    /// 当阈值为NULL或≤0时默认向下取整（兼容旧逻辑）。
    /// </summary>
    private static decimal RoundByThreshold(decimal weight, decimal? truncParam, decimal? ceilParam)
    {
        if (ceilParam == null || ceilParam <= 0)
            return Math.Floor(weight);
        var fraction = weight - Math.Floor(weight);
        return fraction >= ceilParam.Value ? Math.Ceiling(weight) : Math.Floor(weight);
    }

    /// <summary>
    /// 分段进位：向上取整到步进值(ceilParam)的倍数。
    /// 如 ceilParam=0.5, 则 1.1→1.5, 1.6→2.0。
    /// 当步进值为NULL或≤0时回退到旧的固定分段逻辑。
    /// </summary>
    private static decimal RoundBySegment(decimal weight, decimal? truncParam, decimal? ceilParam)
    {
        if (ceilParam == null || ceilParam <= 0)
            return RoundBySegmentLegacy(weight);
        return Math.Ceiling(weight / ceilParam.Value) * ceilParam.Value;
    }

    /// <summary>
    /// 旧版分段进位：0-1kg按0.1进位，1-10kg按0.5进位，10kg+按1进位
    /// </summary>
    private static decimal RoundBySegmentLegacy(decimal weight)
    {
        if (weight <= 1m)
            return Math.Ceiling(weight * 10m) / 10m;
        else if (weight <= 10m)
            return Math.Ceiling(weight * 2m) / 2m;
        else
            return Math.Ceiling(weight);
    }

    /// <summary>
    /// 进百位：向上取整到指定基数(ceilParam, 默认100)的倍数。
    /// 如 ceilParam=100, 则 150→200, 99→100。
    /// </summary>
    private static decimal RoundByHundredth(decimal weight, decimal? truncParam, decimal? ceilParam)
    {
        var divisor = ceilParam ?? 100m;
        if (divisor <= 0) divisor = 100m;
        return Math.Ceiling(weight / divisor) * divisor;
    }
}
