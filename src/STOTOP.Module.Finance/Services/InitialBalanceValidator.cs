namespace STOTOP.Module.Finance.Services;

/// <summary>
/// 期初余额试算平衡校验（纯函数，便于单测）。
/// 全量快照语义：对账套全部末级科目，期初借/贷取「本次提交值 → 库中现值 → 0」，比较 Σ借 与 Σ贷。
/// </summary>
public static class InitialBalanceValidator
{
    /// <summary>金额 2 位小数容差。</summary>
    public const decimal Tolerance = 0.005m;

    /// <summary>
    /// 计算「提交覆盖后」全部末级科目期初的借方合计、贷方合计。
    /// 每个末级科目取 submitted 命中值，否则 existing 命中值，否则 (0,0)。
    /// </summary>
    public static (decimal Debit, decimal Credit) ComputeTotals(
        IEnumerable<long> leafAccountIds,
        IReadOnlyDictionary<long, (decimal Debit, decimal Credit)> existing,
        IReadOnlyDictionary<long, (decimal Debit, decimal Credit)> submitted)
    {
        decimal totalDebit = 0m, totalCredit = 0m;
        foreach (var id in leafAccountIds)
        {
            (decimal Debit, decimal Credit) v =
                submitted.TryGetValue(id, out var s) ? s
                : existing.TryGetValue(id, out var e) ? e
                : (0m, 0m);
            totalDebit += v.Debit;
            totalCredit += v.Credit;
        }
        return (totalDebit, totalCredit);
    }

    /// <summary>差额在容差内即视为平衡。</summary>
    public static bool IsBalanced(decimal difference) => Math.Abs(difference) < Tolerance;
}
