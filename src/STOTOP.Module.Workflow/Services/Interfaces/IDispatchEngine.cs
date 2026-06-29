using STOTOP.Module.Workflow.DTOs;
using STOTOP.Module.Workflow.Entities;

namespace STOTOP.Module.Workflow.Services.Interfaces;

public interface IDispatchEngine
{
    // 根据规则决定如何派发工作项
    Task<DispatchResult> DispatchAsync(long workItemId);

    // 查找匹配的派发规则
    Task<WfDispatchRule?> FindMatchingRuleAsync(long orgId, string module, string bizType);

    // 超时检查（由 Hangfire Job 调用）
    Task ProcessTimeoutsAsync();

    // 升级处理
    Task EscalateAsync(long workItemId);
}
