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
    private readonly IOrgContextAccessor _orgContext;
    private readonly ITenantResolver _tenantResolver;
    private readonly ILogger<WorkItemTimeoutJob> _logger;

    public WorkItemTimeoutJob(
        IDispatchEngine dispatchEngine,
        IOrgContextAccessor orgContext,
        ITenantResolver tenantResolver,
        ILogger<WorkItemTimeoutJob> logger)
    {
        _dispatchEngine = dispatchEngine;
        _orgContext = orgContext;
        _tenantResolver = tenantResolver;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        // Hangfire 无 HttpContext，显式设根租户上下文以过 fail-closed 硬墙（读不空、写回填 FTenantId）。
        _orgContext.CurrentTenantId = _tenantResolver.GetRootTenantId();

        _logger.LogInformation("开始执行工作项超时检查...");
        try
        {
            await _dispatchEngine.ProcessTimeoutsAsync();
            _logger.LogInformation("工作项超时检查完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "工作项超时处理 Job 执行失败");
            throw; // 让 Hangfire 自动重试
        }
    }
}
