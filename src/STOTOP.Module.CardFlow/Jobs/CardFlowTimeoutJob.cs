using Hangfire;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Services;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Hubs;
using STOTOP.Module.CardFlow.Models;
using STOTOP.Module.CardFlow.Services.Interfaces;

namespace STOTOP.Module.CardFlow.Jobs;

/// <summary>
/// CardFlow 节点超时检查（M8-C 升级为超时升级链）。两段逻辑均在同一 per-tenant 循环内逐节点执行：
/// 1) 一次性超时标记（FIsTimeout + ActionLog + SignalR 推送）——行为与升级前完全一致，未配置升级链的节点只走这条路径。
/// 2) 若节点定义配置了 FTimeoutActionJson（见 Models.TimeoutActionConfig），按 elapsedHours/timeoutHours 比例
///    取当前应执行的最高级别，逐级执行 remind / autoApprove / autoReject / escalate；FTimeoutActionLevel 做幂等
///    高水位标记（已执行过的级别不重复执行，即便同一 tick 内或跨 tick 重复调度）。
/// </summary>
public class CardFlowTimeoutJob
{
    private readonly STOTOPDbContext _dbContext;
    private readonly IHubContext<CardFlowHub> _hubContext;
    private readonly INotificationDispatcher _notificationDispatcher;
    private readonly IFlowEngineService _flowEngine;
    private readonly ITenantIterationService _iteration;
    private readonly ILogger<CardFlowTimeoutJob> _logger;

    public CardFlowTimeoutJob(
        STOTOPDbContext dbContext,
        IHubContext<CardFlowHub> hubContext,
        INotificationDispatcher notificationDispatcher,
        IFlowEngineService flowEngine,
        ITenantIterationService iteration,
        ILogger<CardFlowTimeoutJob> logger)
    {
        _dbContext = dbContext;
        _hubContext = hubContext;
        _notificationDispatcher = notificationDispatcher;
        _flowEngine = flowEngine;
        _iteration = iteration;
        _logger = logger;
    }

    /// <summary>并发守卫：同一时刻只允许一个实例执行，避免多 Hangfire worker 或调度重叠导致同一节点被重复处理。</summary>
    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    public async Task ExecuteAsync()
    {
        _logger.LogInformation("CardFlow 节点超时检查开始");

        // 多客户 per-tenant 迭代：逐活跃租户各查各自节点超时。CfStageInstance 本身无租户列 →
        // 经 CfCard(ITenantScoped) 归属间接收敛(EXISTS 子查询)，使每租户只处理自己卡片的节点、且跳过停用租户。
        // 单租户失败由地基隔离并记日志。
        await _iteration.ForEachActiveTenantAsync(async _ =>
        {
        try
        {
            // 查询当前租户 active 且配置了超时的节点实例（经卡片归属 EXISTS 收敛到本租户）
            var activeInstances = await _dbContext.Set<CfStageInstance>()
                .Where(si => si.FStatus == "active" && si.FActivatedTime != null
                    && _dbContext.Set<CfCard>().Any(c => c.FID == si.FCardId))
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

                // ---- 一次性超时标记（升级前既有行为原样保留）：逐节点独立 Save+Clear，不再等到本轮结束批量
                // 落库，以便紧随其后的升级链在干净的 ChangeTracker 上调用引擎方法，避免同主键双实例跟踪冲突 ----
                if (now > deadline && !instance.FIsTimeout)
                {
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

                    await _dbContext.SaveChangesAsync();
                    _dbContext.ChangeTracker.Clear();
                }

                // ---- M8-C 超时升级链：仅当节点定义配置了 FTimeoutActionJson 时生效；
                // null/空/非法 JSON → TimeoutActionConfig.Parse 返回 null，本节点保持向后兼容（只走上面一次性标记）----
                var actionConfig = TimeoutActionConfig.Parse(stageDef.FTimeoutActionJson);
                if (actionConfig == null) continue;

                var elapsedHours = (now - instance.FActivatedTime!.Value).TotalHours;
                var applicableLevel = actionConfig.GetApplicableLevel(elapsedHours, timeoutHours);
                if (applicableLevel == null) continue;

                // 幂等：已执行过≥本级别的动作则跳过，避免同一 tick 或跨 tick 对同一级别重复执行
                if (applicableLevel.Multiplier <= (instance.FTimeoutActionLevel ?? 0)) continue;

                try
                {
                    switch (applicableLevel.Action)
                    {
                        case "remind":
                            await RemindStageAsync(instance, applicableLevel.Multiplier);
                            break;
                        case "autoApprove":
                            await _flowEngine.SystemAutoApproveStageAsync(instance.FID, applicableLevel.Multiplier,
                                $"节点「{instance.FStageName}」超时{applicableLevel.Multiplier}倍未处理，系统自动通过");
                            break;
                        case "autoReject":
                            await _flowEngine.SystemAutoRejectStageAsync(instance.FID, applicableLevel.Multiplier,
                                $"节点「{instance.FStageName}」超时{applicableLevel.Multiplier}倍未处理，系统自动拒绝");
                            break;
                        case "escalate":
                            await _flowEngine.EscalateStageAsync(instance.FID, applicableLevel.Multiplier,
                                $"节点「{instance.FStageName}」超时{applicableLevel.Multiplier}倍未处理，升级至上级");
                            break;
                        default:
                            // 未知动作类型：仍需推进高水位标记并落库，否则每个 tick 都会重新命中同一级别，
                            // 导致无限重试与日志刷屏（与 remind/autoApprove/autoReject/escalate 一致，记录一次后跳过）。
                            _logger.LogWarning(
                                "超时升级链未知动作类型，已记录级别并跳过: StageInstanceId={StageId}, Action={Action}",
                                instance.FID, applicableLevel.Action);
                            _dbContext.Attach(instance);
                            instance.FTimeoutActionLevel = applicableLevel.Multiplier;
                            await _dbContext.SaveChangesAsync();
                            break;
                    }
                }
                finally
                {
                    // 引擎方法各自开事务提交，但不清理 ChangeTracker；每次动作调度后统一清理，
                    // 避免下一实例的 Attach/查询与本次残留跟踪实体撞主键冲突。
                    _dbContext.ChangeTracker.Clear();
                }
            }

            if (timeoutCount > 0)
            {
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
        }, "cardflow-stage-timeout");
    }

    /// <summary>
    /// 超时升级链-remind：对节点当前全部 pending 待办重推通知（同 StageTimeoutReminderJob 的推送链路），
    /// 不涉及卡片状态流转，无需走引擎事务；独立 Attach+Save 落 FTimeoutActionLevel 幂等标记。
    /// </summary>
    private async Task RemindStageAsync(CfStageInstance instance, int level)
    {
        var pendingTodoIds = await _dbContext.Set<CfTodoItem>()
            .Where(t => t.FStageInstanceId == instance.FID && t.FStatus == "pending")
            .Select(t => t.FID)
            .ToListAsync();
        foreach (var todoId in pendingTodoIds)
        {
            await _notificationDispatcher.DispatchCreateTodoAsync(todoId);
        }

        _dbContext.Attach(instance);
        instance.FTimeoutActionLevel = level;
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "超时升级-提醒: CardId={CardId}, StageInstanceId={StageId}, Level={Level}, 待办数={TodoCount}",
            instance.FCardId, instance.FID, level, pendingTodoIds.Count);
    }
}
