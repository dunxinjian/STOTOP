using Hangfire;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Services;
using STOTOP.Module.Workflow.Services.Interfaces;

namespace STOTOP.Module.Workflow.Jobs;

/// <summary>WF工作项超时检查定时任务（每5分钟执行）</summary>
[AutomaticRetry(Attempts = 3)]
public class WorkItemTimeoutJob
{
    private readonly IDispatchEngine _dispatchEngine;
    private readonly ITenantIterationService _iteration;
    private readonly ILogger<WorkItemTimeoutJob> _logger;

    public WorkItemTimeoutJob(
        IDispatchEngine dispatchEngine,
        ITenantIterationService iteration,
        ILogger<WorkItemTimeoutJob> logger)
    {
        _dispatchEngine = dispatchEngine;
        _iteration = iteration;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        _logger.LogInformation("开始执行工作项超时检查...");
        // 多客户 per-tenant 迭代：逐活跃租户各查各自超时工作项（WfWorkItem 是 ITenantScoped，过滤器按租户收敛）。
        // 单租户下只循环 1 次、行为不变；单租户失败已被地基隔离并记日志。
        await _iteration.ForEachActiveTenantAsync(async _ =>
        {
            await _dispatchEngine.ProcessTimeoutsAsync();
        }, "workflow-item-timeout");
        _logger.LogInformation("工作项超时检查完成");
    }
}
