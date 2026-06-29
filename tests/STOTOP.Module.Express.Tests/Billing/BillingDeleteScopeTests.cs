using System.Collections.Generic;
using STOTOP.Module.Express.Services.Billing;
using Xunit;

namespace STOTOP.Module.Express.Tests.Billing;

/// <summary>
/// 计费结果删除范围回归测试。
/// 锁定：重跑去重的 DELETE 必须限定本批次本运单（Phase B 还需限定失败状态），
/// 否则会发生「同批两阶段互删」「跨批次误删」导致成功行丢失。
/// </summary>
public class BillingDeleteScopeTests
{
    private static readonly IReadOnlySet<string> WaybillSto123 =
        new HashSet<string> { "STO123" };

    // 正路径 & 风险根因：不限状态(null)删除会命中本批次本运单的成功行。
    // 对 Phase A 而言这是预期（重跑时替换自己的旧成功行）；
    // 但若 Phase B 也不限状态，就会误删 Phase A 刚写入的成功行（数据丢失）。
    [Fact]
    public void Unscoped_delete_hits_same_batch_success_row()
    {
        Assert.True(BillingDeleteScope.WouldDeleteRow(
            rowBatchId: 82, rowWaybillNo: "STO123", rowCalcStatus: 1,
            deleteBatchId: 82, deleteWaybillNos: WaybillSto123, deleteCalcStatus: null));
    }

    // 修复：Phase B 限定状态=2 → 成功行(状态1)不被命中，存活。
    [Fact]
    public void PhaseB_status_scoped_delete_spares_success_row()
    {
        Assert.False(BillingDeleteScope.WouldDeleteRow(
            rowBatchId: 82, rowWaybillNo: "STO123", rowCalcStatus: 1,
            deleteBatchId: 82, deleteWaybillNos: WaybillSto123, deleteCalcStatus: 2));
    }

    // 跨批次误删：批次81的历史成功行，不应被批次82的删除命中（修复后行为）。
    [Fact]
    public void Delete_must_not_cross_batch()
    {
        Assert.False(BillingDeleteScope.WouldDeleteRow(
            rowBatchId: 81, rowWaybillNo: "STO123", rowCalcStatus: 1,
            deleteBatchId: 82, deleteWaybillNos: WaybillSto123, deleteCalcStatus: null));
    }

    // 本批次本运单的失败行：Phase B 正常清理（重跑去重的预期行为）。
    [Fact]
    public void PhaseB_delete_removes_same_batch_failure_row()
    {
        Assert.True(BillingDeleteScope.WouldDeleteRow(
            rowBatchId: 82, rowWaybillNo: "STO123", rowCalcStatus: 2,
            deleteBatchId: 82, deleteWaybillNos: WaybillSto123, deleteCalcStatus: 2));
    }

    // 不在待删名单的运单：永不命中。
    [Fact]
    public void Delete_skips_waybills_not_in_set()
    {
        Assert.False(BillingDeleteScope.WouldDeleteRow(
            rowBatchId: 82, rowWaybillNo: "STO999", rowCalcStatus: 2,
            deleteBatchId: 82, deleteWaybillNos: WaybillSto123, deleteCalcStatus: 2));
    }
}
