using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Services;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services.Interfaces;

namespace STOTOP.Module.CardFlow.Jobs;

public class PushRetryJob
{
    private const int MaxRetryCount = 3;

    private readonly STOTOPDbContext _dbContext;
    private readonly INotificationDispatcher _notificationDispatcher;
    private readonly ITenantScopeFactory _tenantScope;
    private readonly ILogger<PushRetryJob> _logger;

    public PushRetryJob(
        STOTOPDbContext dbContext,
        INotificationDispatcher notificationDispatcher,
        ITenantScopeFactory tenantScope,
        ILogger<PushRetryJob> logger)
    {
        _dbContext = dbContext;
        _notificationDispatcher = notificationDispatcher;
        _tenantScope = tenantScope;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        // 保持 global：一次批处理跨全部租户的失败推送 top50（推送重试是基础设施、非业务，不做 per-tenant 迭代）。
        // 查询用 IgnoreQueryFilters 跨租户扫描；但每条在其【自身租户】上下文内重试（见下），
        // 避免 RetryPush 内部写入（如分发日志）按错误租户回填 → 串租户。
        _logger.LogInformation("CardFlow 推送失败重试任务开始");

        try
        {
            // 查询推送失败且未取消的待办项
            var failedItems = await _dbContext.Set<CfTodoItem>()
                .IgnoreQueryFilters() // 跨组织执行
                .Where(t => t.FPushStatus == "failed" && t.FStatus != "cancelled" && t.FRetryCount < MaxRetryCount)
                .OrderBy(t => t.FCreatedTime)
                .Take(50) // 每次最多处理50条
                .ToListAsync();

            if (!failedItems.Any())
            {
                _logger.LogDebug("没有需要重试的推送失败待办");
                return;
            }

            var successCount = 0;
            var failCount = 0;
            var permanentFailCount = 0;

            foreach (var todo in failedItems)
            {
                // 整段 per-item 工作单元（改计数→重试→查状态→改状态→落库）都在该待办【自身租户】上下文内，
                // 杜绝 RetryPush 内部写入 / 状态回写按错误租户回填或跨租户串扰；每条独立落库，作用域退出即复位。
                using (_tenantScope.Enter(todo.FTenantId, "cardflow-push-retry"))
                {
                    _dbContext.Attach(todo);
                    todo.FRetryCount++;

                    try
                    {
                        await _notificationDispatcher.RetryPushAsync(todo.FID);

                        // 重新查询推送状态
                        var updated = await _dbContext.Set<CfTodoItem>()
                            .IgnoreQueryFilters()
                            .FirstOrDefaultAsync(t => t.FID == todo.FID);

                        if (updated?.FPushStatus == "success")
                        {
                            successCount++;
                            _logger.LogInformation("推送重试成功: TodoItemId={Id}, RetryCount={Count}",
                                todo.FID, todo.FRetryCount);
                        }
                        else
                        {
                            failCount++;
                            // 超过最大重试次数，标记永久失败
                            if (todo.FRetryCount >= MaxRetryCount)
                            {
                                todo.FPushStatus = "permanently_failed";
                                permanentFailCount++;
                                _logger.LogWarning("推送重试达到上限，标记永久失败: TodoItemId={Id}", todo.FID);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        failCount++;
                        _logger.LogError(ex, "推送重试异常: TodoItemId={Id}, RetryCount={Count}",
                            todo.FID, todo.FRetryCount);

                        if (todo.FRetryCount >= MaxRetryCount)
                        {
                            todo.FPushStatus = "permanently_failed";
                            permanentFailCount++;
                            _logger.LogWarning("推送重试达到上限，标记永久失败: TodoItemId={Id}", todo.FID);
                        }
                    }

                    await _dbContext.SaveChangesAsync();  // 每条在其租户上下文内落库
                }
            }

            _logger.LogInformation(
                "CardFlow 推送失败重试完成: 成功={Success}, 失败={Fail}, 永久失败={PermanentFail}",
                successCount, failCount, permanentFailCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CardFlow 推送失败重试任务异常");
            throw;
        }
    }
}
