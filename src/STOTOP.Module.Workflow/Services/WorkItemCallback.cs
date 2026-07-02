using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Services;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Workflow.DTOs;
using STOTOP.Module.Workflow.Entities;
using STOTOP.Module.Workflow.Services.Interfaces;

namespace STOTOP.Module.Workflow.Services;

public class WorkItemCallback : IWorkItemCallback
{
    private readonly ConcurrentDictionary<string, Func<CallbackContext, Task>> _handlers = new();
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WorkItemCallback> _logger;

    public WorkItemCallback(IServiceProvider serviceProvider, ILogger<WorkItemCallback> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task OnCompletedAsync(long workItemId, string? result)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<STOTOPDbContext>();

        // 子作用域的 IOrgContextAccessor：HTTP 来源经 HttpContext.Items 已带调用方租户上下文；非 HTTP 来源(任务超时等)为空。
        var orgContext = scope.ServiceProvider.GetService<IOrgContextAccessor>();
        var workItem = await ResolveWorkItemAsync(db, orgContext, workItemId);
        if (workItem == null)
        {
            _logger.LogWarning("回调失败：工作项 {WorkItemId} 不存在", workItemId);
            return;
        }

        var key = BuildHandlerKey(workItem.FModule, workItem.FBizType);
        if (_handlers.TryGetValue(key, out var handler))
        {
            var context = new CallbackContext
            {
                WorkItemId = workItemId,
                Module = workItem.FModule,
                BizType = workItem.FBizType,
                BizId = workItem.FBizId,
                ChainId = workItem.FChainId,
                DataScopeId = workItem.FDataScopeId,
                Result = result
            };

            try
            {
                await handler(context);
                _logger.LogInformation("工作项 {WorkItemId} 完成回调执行成功，Module: {Module}, BizType: {BizType}", workItemId, workItem.FModule, workItem.FBizType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "工作项 {WorkItemId} 完成回调执行异常", workItemId);
            }
        }
        else
        {
            _logger.LogDebug("工作项 {WorkItemId} 无注册的完成回调处理器（{Key}）", workItemId, key);
        }
    }

    public async Task OnCancelledAsync(long workItemId, string? reason)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<STOTOPDbContext>();

        var orgContext = scope.ServiceProvider.GetService<IOrgContextAccessor>();
        var workItem = await ResolveWorkItemAsync(db, orgContext, workItemId);
        if (workItem == null)
        {
            _logger.LogWarning("取消回调失败：工作项 {WorkItemId} 不存在", workItemId);
            return;
        }

        var key = BuildHandlerKey(workItem.FModule, workItem.FBizType);
        if (_handlers.TryGetValue(key, out var handler))
        {
            var context = new CallbackContext
            {
                WorkItemId = workItemId,
                Module = workItem.FModule,
                BizType = workItem.FBizType,
                BizId = workItem.FBizId,
                ChainId = workItem.FChainId,
                DataScopeId = workItem.FDataScopeId,
                Result = reason
            };

            try
            {
                await handler(context);
                _logger.LogInformation("工作项 {WorkItemId} 取消回调执行成功", workItemId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "工作项 {WorkItemId} 取消回调执行异常", workItemId);
            }
        }
        else
        {
            _logger.LogDebug("工作项 {WorkItemId} 无注册的取消回调处理器（{Key}）", workItemId, key);
        }
    }

    public void RegisterHandler(string module, string bizType, Func<CallbackContext, Task> handler)
    {
        var key = BuildHandlerKey(module, bizType);
        _handlers[key] = handler;
        _logger.LogInformation("注册回调处理器: {Key}", key);
    }

    /// <summary>
    /// 按主键取工作项并按需设子作用域租户上下文。
    /// 有租户上下文（HTTP 来源经 HttpContext.Items 可见）→ 正常过滤读，强制隔离在调用方租户、杜绝越权读他租户工作项；
    /// 无租户上下文（任务超时等非 HTTP 来源）→ 绕全局过滤器 bootstrap 取工作项、采用其租户设子作用域，
    /// 令处理器读写 ITenantScoped 不被 fail-closed 硬墙挡。
    /// </summary>
    private static async Task<WfWorkItem?> ResolveWorkItemAsync(STOTOPDbContext db, IOrgContextAccessor? orgContext, long workItemId)
    {
        if (orgContext?.CurrentTenantId != null)
            return await db.Set<WfWorkItem>().FirstOrDefaultAsync(w => w.FID == workItemId);

        var workItem = await db.Set<WfWorkItem>().IgnoreQueryFilters().FirstOrDefaultAsync(w => w.FID == workItemId);
        if (workItem != null && orgContext != null)
        {
            orgContext.CurrentTenantId = workItem.FTenantId;
            orgContext.CurrentOrgId = workItem.FOrgId;
        }
        return workItem;
    }

    private static string BuildHandlerKey(string? module, string? bizType)
    {
        return $"{module ?? "*"}:{bizType ?? "*"}";
    }
}
