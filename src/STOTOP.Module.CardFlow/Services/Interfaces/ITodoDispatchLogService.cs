namespace STOTOP.Module.CardFlow.Services.Interfaces;

/// <summary>
/// 待办分发日志服务（阶段4E·R4）：分发时记 taskId→(租户,待办) 权威绑定；回调时据 taskId 精确定位待办 + 幂等去重。
/// </summary>
public interface ITodoDispatchLogService
{
    /// <summary>分发成功后登记（按 待办+渠道 幂等 upsert，更新 taskId）。best-effort 调用方吞异常。</summary>
    Task RecordDispatchAsync(long todoItemId, long tenantId, string channel, string? externalTaskId, string? corpId);

    /// <summary>
    /// 回调开始处理：据外部 taskId 从分发日志定位待办。
    /// 返回 (TodoItemId, AlreadyProcessed)：
    /// · TodoItemId=null → 无分发记录（伪造/历史遗留，调用方走 legacy 匹配并告警）；
    /// · AlreadyProcessed=true → 同一事件已处理过（重放），调用方跳过（幂等 no-op）；
    /// · 否则标记本次事件已处理并返回待办ID，调用方更新该待办。
    /// </summary>
    Task<(long? TodoItemId, bool AlreadyProcessed)> TryBeginCallbackAsync(string externalTaskId, string eventType);
}
