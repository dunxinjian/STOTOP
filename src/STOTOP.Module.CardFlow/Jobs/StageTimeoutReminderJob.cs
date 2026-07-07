using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Services;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services.Interfaces;

namespace STOTOP.Module.CardFlow.Jobs;

/// <summary>
/// M2-7 节点一级超时提醒：active 人工节点实例超过定义的 F超时小时数 且未提醒过（F超时提醒时间 为 null）时，
/// 给该实例所有未处理待办重推通知（与催办 UrgeAsync 同链：INotificationDispatcher.DispatchCreateTodoAsync），
/// 并置 F超时提醒时间=now 保证幂等。区别于 CardFlowTimeoutJob（标记 FIsTimeout + SignalR 推送），本 Job 只做一次性提醒。
/// </summary>
public class StageTimeoutReminderJob
{
    private readonly STOTOPDbContext _dbContext;
    private readonly INotificationDispatcher _notificationDispatcher;
    private readonly ITenantIterationService _iteration;
    private readonly ILogger<StageTimeoutReminderJob> _logger;

    public StageTimeoutReminderJob(
        STOTOPDbContext dbContext,
        INotificationDispatcher notificationDispatcher,
        ITenantIterationService iteration,
        ILogger<StageTimeoutReminderJob> logger)
    {
        _dbContext = dbContext;
        _notificationDispatcher = notificationDispatcher;
        _iteration = iteration;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        _logger.LogInformation("CardFlow 节点超时提醒检查开始");

        // per-tenant 迭代：CfStageInstance 无租户列，经 CfCard(ITenantScoped) EXISTS 间接收敛到当前租户
        await _iteration.ForEachActiveTenantAsync(async _ =>
        {
            var now = DateTime.Now;
            var candidates = await (
                from si in _dbContext.Set<CfStageInstance>()
                from sd in _dbContext.Set<CfStageDefinition>().Where(d => d.FID == si.FStageDefinitionId)
                where si.FStatus == "active"
                    && si.FType != "auto"
                    && si.FActivatedTime != null
                    && si.FTimeoutRemindedAt == null
                    && sd.FTimeoutHours != null && sd.FTimeoutHours > 0
                    && _dbContext.Set<CfCard>().Any(c => c.FID == si.FCardId)
                select new { Instance = si, TimeoutHours = sd.FTimeoutHours!.Value })
                .ToListAsync();

            var remindedCount = 0;
            foreach (var item in candidates)
            {
                var deadline = item.Instance.FActivatedTime!.Value.AddHours(item.TimeoutHours);
                if (now <= deadline) continue;

                var pendingTodoIds = await _dbContext.Set<CfTodoItem>()
                    .Where(t => t.FStageInstanceId == item.Instance.FID && t.FStatus == "pending")
                    .Select(t => t.FID)
                    .ToListAsync();
                foreach (var todoId in pendingTodoIds)
                {
                    await _notificationDispatcher.DispatchCreateTodoAsync(todoId);
                }

                // 全局 NoTracking 下 Attach 后改属性由变更检测落库（与 CardFlowTimeoutJob 同款）
                _dbContext.Attach(item.Instance);
                item.Instance.FTimeoutRemindedAt = now;
                remindedCount++;

                _logger.LogInformation(
                    "节点超时提醒: CardId={CardId}, StageInstanceId={StageId}, StageName={StageName}, 待办数={TodoCount}",
                    item.Instance.FCardId, item.Instance.FID, item.Instance.FStageName, pendingTodoIds.Count);
            }

            if (remindedCount > 0)
            {
                await _dbContext.SaveChangesAsync();
                _logger.LogInformation("CardFlow 节点超时提醒完成，提醒 {Count} 个节点", remindedCount);
            }
        }, "cardflow-stage-timeout-reminder");
    }
}
