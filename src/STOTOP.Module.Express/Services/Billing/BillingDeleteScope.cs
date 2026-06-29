namespace STOTOP.Module.Express.Services.Billing;

/// <summary>
/// 计费结果删除范围的纯判定函数——把删除 SQL 的 WHERE 语义在 C# 里等价表达，
/// 作为删除范围的可执行规格供单测使用（DELETE 依赖临时表，无法脱离真实库直测）。
/// 它描述删除范围的目标语义（按 批次 + 运单号集合 + 可选计算状态 限定），
/// BillingBulkWriter.DeleteExistingResults 与 PricingPlugin.DeleteOldBillingResultsAsync
/// 的 DELETE 谓词应与之保持一致；改动任一侧时须同步另一侧。
/// </summary>
public static class BillingDeleteScope
{
    /// <summary>
    /// 判定某计费结果行是否会被「按运单号 + 批次（+ 可选状态）」的删除命中。
    /// </summary>
    /// <param name="rowBatchId">结果行所属批次</param>
    /// <param name="rowWaybillNo">结果行运单编号</param>
    /// <param name="rowCalcStatus">结果行计算状态（1=成功，2=失败）</param>
    /// <param name="deleteBatchId">本次删除限定的批次</param>
    /// <param name="deleteWaybillNos">本次删除限定的运单编号集合</param>
    /// <param name="deleteCalcStatus">本次删除限定的计算状态；null 表示不限状态</param>
    public static bool WouldDeleteRow(
        long rowBatchId,
        string rowWaybillNo,
        int rowCalcStatus,
        long deleteBatchId,
        IReadOnlySet<string> deleteWaybillNos,
        int? deleteCalcStatus)
    {
        if (rowBatchId != deleteBatchId) return false;
        if (!deleteWaybillNos.Contains(rowWaybillNo)) return false;
        if (deleteCalcStatus.HasValue && rowCalcStatus != deleteCalcStatus.Value) return false;
        return true;
    }
}
