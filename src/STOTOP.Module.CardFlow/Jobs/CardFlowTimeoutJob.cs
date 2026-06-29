using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Hubs;
using STOTOP.Module.CardFlow.Services.Interfaces;

namespace STOTOP.Module.CardFlow.Jobs;

public class CardFlowTimeoutJob
{
    private readonly STOTOPDbContext _dbContext;
    private readonly IHubContext<CardFlowHub> _hubContext;
    private readonly INotificationDispatcher _notificationDispatcher;
    private readonly ILogger<CardFlowTimeoutJob> _logger;

    public CardFlowTimeoutJob(
        STOTOPDbContext dbContext,
        IHubContext<CardFlowHub> hubContext,
        INotificationDispatcher notificationDispatcher,
        ILogger<CardFlowTimeoutJob> logger)
    {
        _dbContext = dbContext;
        _hubContext = hubContext;
        _notificationDispatcher = notificationDispatcher;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        _logger.LogInformation("CardFlow 节点超时检查开始");

        try
        {
            // 查询所有 active 且配置了超时的节点实例
            var activeInstances = await _dbContext.Set<CfStageInstance>()
                .Where(si => si.FStatus == "active" && si.FActivatedTime != null)
                .ToListAsync();

            if (!activeInstances.Any())
            {
                _logger.LogDebug("没有 active 状态的节点实例");
                return;
            }

            // 获取关联的节点定义（含超时配置）
            var stageDefIds = activeInstances
                .Where(si => si.FStageDefinitionId.HasValue)
                .Select(si => si.FStageDefinitionId!.Value)
                .Distinct()
                .ToList();

            var stageDefs = await _dbContext.Set<CfStageDefinition>()
                .Where(sd => stageDefIds.Contains(sd.FID) && sd.FTimeoutHours != null && sd.FTimeoutHours > 0)
                .ToDictionaryAsync(sd => sd.FID);

            var now = DateTime.Now;
            var timeoutCount = 0;

            foreach (var instance in activeInstances)
            {
                if (!instance.FStageDefinitionId.HasValue) continue;
                if (!stageDefs.TryGetValue(instance.FStageDefinitionId.Value, out var stageDef)) continue;

                var timeoutHours = stageDef.FTimeoutHours!.Value;
                var deadline = instance.FActivatedTime!.Value.AddHours(timeoutHours);

                if (now <= deadline) continue;
                if (instance.FIsTimeout) continue; // 已标记超时，跳过首次标记

                // 标记超时
                _dbContext.Attach(instance);
                instance.FIsTimeout = true;
                timeoutCount++;

                // 记录超时日志
                var actionLog = new CfActionLog
                {
                    FCardId = instance.FCardId,
                    FStageInstanceId = instance.FID,
                    FActionType = "timeout",
                    FOperatorId = 0,
                    FOperatorName = "系统",
                    FOperationTime = now,
                    FOpinion = $"节点「{instance.FStageName}」已超时（超时阈值: {timeoutHours}小时）",
                    FDetailJson = global::System.Text.Json.JsonSerializer.Serialize(new
                    {
                        stageInstanceId = instance.FID,
                        activatedTime = instance.FActivatedTime,
                        timeoutHours,
                        actualHours = (now - instance.FActivatedTime.Value).TotalHours
                    })
                };
                _dbContext.Set<CfActionLog>().Add(actionLog);

                // 根据超时倍数决定通知级别
                var overHours = (now - deadline).TotalHours;
                string level;
                if (overHours >= 2 * timeoutHours)
                    level = "critical";   // 3x 超时
                else if (overHours >= timeoutHours)
                    level = "warning";    // 2x 超时
                else
                    level = "info";       // 1x 超时

                // 通过 SignalR 推送超时通知
                await _hubContext.Clients.Group($"card-{instance.FCardId}").SendAsync("StageTimeout", new
                {
                    cardId = instance.FCardId,
                    stageInstanceId = instance.FID,
                    stageName = instance.FStageName,
                    level,
                    timeoutHours,
                    activatedTime = instance.FActivatedTime
                });

                // 推送到监控频道
                await _hubContext.Clients.Group("cardflow-monitor").SendAsync("StageTimeout", new
                {
                    cardId = instance.FCardId,
                    stageInstanceId = instance.FID,
                    stageName = instance.FStageName,
                    level,
                    timeoutHours,
                    activatedTime = instance.FActivatedTime
                });

                _logger.LogWarning(
                    "节点超时: CardId={CardId}, StageInstanceId={StageId}, StageName={StageName}, Level={Level}",
                    instance.FCardId, instance.FID, instance.FStageName, level);
            }

            if (timeoutCount > 0)
            {
                await _dbContext.SaveChangesAsync();
                _logger.LogInformation("CardFlow 节点超时检查完成，标记 {Count} 个超时节点", timeoutCount);
            }
            else
            {
                _logger.LogDebug("CardFlow 节点超时检查完成，无新增超时");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CardFlow 节点超时检查异常");
            throw;
        }
    }
}
