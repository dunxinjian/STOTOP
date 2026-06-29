using STOTOP.Module.CardFlow.Entities;

namespace STOTOP.Module.CardFlow.Services.Interfaces;

/// <summary>
/// 质量问题派发服务接口 - 按错误类型分组创建 WorkItem 并推送通知
/// 迁移自 DataCenter.Services.IQualityDispatchService，Task 10
/// </summary>
public interface IQualityDispatchService
{
    /// <summary>
    /// 批量派发质量问题。按错误类型分组，每组创建一个 WorkItem 并推送。
    /// </summary>
    /// <param name="batchId">批次ID</param>
    /// <param name="orgId">组织ID</param>
    /// <param name="errors">待派发的错误记录列表</param>
    Task DispatchAsync(long batchId, long orgId, IReadOnlyList<CfBatchError> errors);
}
