using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Models.Approval;
using STOTOP.Module.CardFlow.Services.Interfaces;

namespace STOTOP.Module.CardFlow.Services;

/// <summary>
/// 发布后在途迁移（M6 #4，InflightPolicy=migrate）。
/// 只迁「FStatus=active 且当前 active 节点实例的 stageKey 在新版本已被删除」的卡片：
/// 关旧实例（cancelled）→ 在新版本序列中取「原排序其后的第一个节点」重建实例 → 卡片切到新版本。
/// 每张被迁/降级/失败的卡片各记一行 CF版本迁移日志；逐张 try/catch，不整体回滚（发布本身已成功落库）。
///
/// 诚实降级面（不做错误迁移）：
/// - 目标为自动节点：不在发布路径执行插件，记 skipped 日志、卡片保留旧版走完；
/// - 新版本原排序其后无节点：记 skipped 日志、保留旧版；
/// - returned/draft 卡不迁（无 active 实例，resubmit/submit 各有版本语义）；
/// - 处理人解析复用 IApproverResolver + SequentialApprovalRuntime（与引擎 AssignStageHandlersAsync 同一最小路径），
///   不执行 autoDecision 信封；待办直接落行（FPushStatus=pending，外部推送由既有通道跟进）。
/// </summary>
public class FlowVersionMigrationService : IFlowVersionMigrationService
{
    private readonly STOTOPDbContext _dbContext;
    private readonly IApproverResolver _approverResolver;
    private readonly SequentialApprovalRuntime _sequentialRuntime;
    private readonly ILogger<FlowVersionMigrationService> _logger;

    public FlowVersionMigrationService(
        STOTOPDbContext dbContext,
        IApproverResolver approverResolver,
        SequentialApprovalRuntime sequentialRuntime,
        ILogger<FlowVersionMigrationService> logger)
    {
        _dbContext = dbContext;
        _approverResolver = approverResolver;
        _sequentialRuntime = sequentialRuntime;
        _logger = logger;
    }

    public async Task<PublishFlowDefinitionResultDto> MigrateInflightCardsAsync(long definitionId, long operatorId)
    {
        var result = new PublishFlowDefinitionResultDto { InflightPolicy = "migrate" };

        var newVersion = await _dbContext.Set<CfFlowVersion>()
            .FirstOrDefaultAsync(v => v.FFlowDefinitionId == definitionId && v.FIsCurrentVersion)
            ?? throw new InvalidOperationException("当前定义没有已发布版本，无法执行在途迁移");

        var newStages = await _dbContext.Set<CfStageDefinition>()
            .Where(s => s.FFlowVersionId == newVersion.FID)
            .OrderBy(s => s.FSortOrder)
            .ToListAsync();
        var newStageKeys = newStages
            .Select(s => s.FStageKey)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var cards = await _dbContext.Set<CfCard>()
            .Where(c => c.FFlowDefinitionId == definitionId
                && c.FStatus == "active"
                && c.FFlowVersionId != newVersion.FID)
            .ToListAsync();

        foreach (var card in cards)
        {
            var oldVersionId = card.FFlowVersionId;
            if (!card.FCurrentStageInstanceId.HasValue) continue;

            var instance = await _dbContext.Set<CfStageInstance>()
                .FirstOrDefaultAsync(s => s.FID == card.FCurrentStageInstanceId.Value && s.FStatus == "active");
            if (instance == null || !instance.FStageDefinitionId.HasValue) continue; // 无 active 实例 / 动态插入节点：不迁

            var oldStage = await _dbContext.Set<CfStageDefinition>()
                .FirstOrDefaultAsync(d => d.FID == instance.FStageDefinitionId.Value);
            if (oldStage == null) continue;

            // 当前节点在新版本仍存在 → 不动（继续走旧版）
            if (newStageKeys.Contains(oldStage.FStageKey)) continue;

            var target = newStages.FirstOrDefault(s => s.FSortOrder > oldStage.FSortOrder);
            if (target == null)
            {
                await WriteLogAsync(card, oldVersionId, newVersion.FID, oldStage.FStageKey, null,
                    "skipped", "新版本在原节点排序之后无后继节点，卡片保留旧版本继续走完");
                result.SkippedCount++;
                result.Warnings.Add($"卡片 {CardLabel(card)}：新版本无后继节点，保留旧版走完");
                continue;
            }

            if (!string.Equals(target.FType, "human", StringComparison.OrdinalIgnoreCase))
            {
                await WriteLogAsync(card, oldVersionId, newVersion.FID, oldStage.FStageKey, target.FStageKey,
                    "skipped", $"目标节点「{target.FStageName}」为自动节点，暂不支持在途迁移，卡片保留旧版本继续走完");
                result.SkippedCount++;
                result.Warnings.Add($"卡片 {CardLabel(card)}：目标节点为自动节点，保留旧版走完");
                continue;
            }

            try
            {
                // ① 先做只读的处理人解析——失败时卡片保持原状，不产生半截迁移
                var resolveResult = await _approverResolver.ResolveAsync(
                    target, card, ParseCardData(card.FDataJson), card.FOrgId, card.FInitiatorId, newVersion.FFlowSettingsJson);
                if (!resolveResult.Success)
                    throw new InvalidOperationException(resolveResult.ErrorMessage ?? "未解析到有效处理人");

                var approvers = resolveResult.Approvers
                    .OrderBy(a => a.SortOrder)
                    .Select((a, index) => new ResolvedApprover
                    {
                        UserId = a.UserId,
                        UserName = string.IsNullOrWhiteSpace(a.UserName) ? a.UserId.ToString() : a.UserName,
                        Source = a.Source,
                        SortOrder = index + 1
                    })
                    .ToList();
                var assignments = _sequentialRuntime.BuildInitialAssignments(target.FApprovalMode, approvers);

                // ② 建新实例（单独 SaveChanges 取自增 FID；此时尚未改动旧实例/卡片，失败无副作用）
                var newInstance = new CfStageInstance
                {
                    FCardId = card.FID,
                    FStageDefinitionId = target.FID,
                    FStageName = target.FStageName,
                    FType = target.FType,
                    FApprovalMode = target.FApprovalMode,
                    FRound = card.FCurrentRound,
                    FStatus = "active",
                    FActivatedTime = DateTime.Now,
                    FStartTime = DateTime.Now
                };
                _dbContext.Set<CfStageInstance>().Add(newInstance);
                await _dbContext.SaveChangesAsync();

                // ③ 关旧实例 + 取消其未完成处理人/待办、落处理人与待办、卡片切新版本、记日志——一次 SaveChanges 原子提交
                instance.FStatus = "cancelled";
                instance.FCompletedTime = DateTime.Now;
                _dbContext.Entry(instance).State = EntityState.Modified;

                var openAssignees = await _dbContext.Set<CfStageAssignee>()
                    .Where(a => a.FStageInstanceId == instance.FID && (a.FStatus == "pending" || a.FStatus == "waiting"))
                    .ToListAsync();
                foreach (var assignee in openAssignees)
                {
                    assignee.FStatus = "cancelled";
                    _dbContext.Entry(assignee).State = EntityState.Modified;
                }

                var pendingTodos = await _dbContext.Set<CfTodoItem>()
                    .Where(t => t.FStageInstanceId == instance.FID && t.FStatus == "pending")
                    .ToListAsync();
                foreach (var todo in pendingTodos)
                {
                    todo.FStatus = "cancelled";
                    todo.FCompletedTime = DateTime.Now;
                    _dbContext.Entry(todo).State = EntityState.Modified;
                }

                foreach (var assignment in assignments)
                {
                    _dbContext.Set<CfStageAssignee>().Add(new CfStageAssignee
                    {
                        FStageInstanceId = newInstance.FID,
                        FUserId = assignment.UserId,
                        FUserName = assignment.UserName,
                        FSortOrder = assignment.SortOrder,
                        FAssignedTime = DateTime.Now,
                        FStatus = assignment.Status
                    });
                }
                foreach (var assignment in assignments.Where(a => a.Status == "pending"))
                {
                    _dbContext.Set<CfTodoItem>().Add(new CfTodoItem
                    {
                        FCardId = card.FID,
                        FStageInstanceId = newInstance.FID,
                        FHandlerId = assignment.UserId,
                        FHandlerName = assignment.UserName,
                        FTitle = card.FTitle ?? "待办",
                        FType = "todo",
                        FStatus = "pending",
                        FPriority = 3,
                        FPushStatus = "pending",
                        FCreatedTime = DateTime.Now,
                        FOrgId = card.FOrgId
                    });
                }

                card.FFlowVersionId = newVersion.FID;
                card.FCurrentStageInstanceId = newInstance.FID;
                card.FUpdatedTime = DateTime.Now;
                _dbContext.Entry(card).State = EntityState.Modified;

                AddLogEntry(card, oldVersionId, newVersion.FID, oldStage.FStageKey, target.FStageKey,
                    "migrated", $"已迁移至新版本节点「{target.FStageName}」");
                await _dbContext.SaveChangesAsync();

                result.MigratedCount++;
                _logger.LogInformation("在途迁移成功：定义 {DefinitionId} 卡片 {CardId} {OldKey}→{NewKey}（旧版本 {OldVersion}→新版本 {NewVersion}），操作人 {OperatorId}",
                    definitionId, card.FID, oldStage.FStageKey, target.FStageKey, oldVersionId, newVersion.FID, operatorId);
            }
            catch (Exception ex)
            {
                _dbContext.ChangeTracker.Clear();
                _logger.LogError(ex, "在途迁移失败：定义 {DefinitionId} 卡片 {CardId}", definitionId, card.FID);
                AddLogEntry(card, oldVersionId, newVersion.FID, oldStage.FStageKey, target.FStageKey,
                    "failed", Truncate(ex.Message, 500));
                await _dbContext.SaveChangesAsync();
                result.FailedCount++;
                result.Warnings.Add($"卡片 {CardLabel(card)} 迁移失败：{ex.Message}");
            }
        }

        return result;
    }

    private async Task WriteLogAsync(CfCard card, long oldVersionId, long newVersionId,
        string? oldStageKey, string? newStageKey, string resultCode, string message)
    {
        AddLogEntry(card, oldVersionId, newVersionId, oldStageKey, newStageKey, resultCode, message);
        await _dbContext.SaveChangesAsync();
    }

    private void AddLogEntry(CfCard card, long oldVersionId, long newVersionId,
        string? oldStageKey, string? newStageKey, string resultCode, string message)
    {
        _dbContext.Set<CfVersionMigrationLog>().Add(new CfVersionMigrationLog
        {
            FTenantId = card.FTenantId,
            FOrgId = card.FOrgId,
            FFlowDefinitionId = card.FFlowDefinitionId,
            FCardId = card.FID,
            FOldVersionId = oldVersionId,
            FNewVersionId = newVersionId,
            FOldStageKey = oldStageKey,
            FNewStageKey = newStageKey,
            FResult = resultCode,
            FMessage = Truncate(message, 500),
            FCreatedTime = DateTime.Now
        });
    }

    private static string CardLabel(CfCard card)
        => string.IsNullOrWhiteSpace(card.FCardNumber) ? $"#{card.FID}" : card.FCardNumber!;

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];

    private static Dictionary<string, object?> ParseCardData(string? dataJson)
    {
        if (string.IsNullOrWhiteSpace(dataJson)) return new();
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(dataJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        }
        catch
        {
            return new();
        }
    }
}
