namespace STOTOP.Module.CardFlow.AutoPlugin;

/// <summary>
/// AutoPlugin 行级进度上报接口（带里程碑节流）
/// 迁移自 CFAutoPlugin，原迁移自 DataCenter
/// </summary>
public interface IAutoPluginProgressReporter
{
    /// <summary>
    /// 上报行级进度（带里程碑节流，仅在达到 10%/25%/50%/75%/100% 时实际推送）
    /// </summary>
    Task ReportAsync(long batchId, string phase, int current, int total);

    /// <summary>
    /// 强制上报阶段切换（不受节流限制）
    /// </summary>
    Task ReportPhaseAsync(long batchId, string phase, string message);

    /// <summary>
    /// 重置指定批次的里程碑状态（AutoPlugin 切换时调用）
    /// </summary>
    void Reset(long batchId);
}
