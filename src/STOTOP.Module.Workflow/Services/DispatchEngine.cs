using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Workflow.DTOs;
using STOTOP.Module.Workflow.Entities;
using STOTOP.Module.Workflow.Enums;
using STOTOP.Module.Workflow.Services.Interfaces;

namespace STOTOP.Module.Workflow.Services;

public class DispatchEngine : IDispatchEngine
{
    private readonly STOTOPDbContext _db;
    private readonly ILogger<DispatchEngine> _logger;

    public DispatchEngine(STOTOPDbContext db, ILogger<DispatchEngine> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<DispatchResult> DispatchAsync(long workItemId)
    {
        var workItem = await _db.Set<WfWorkItem>().FirstOrDefaultAsync(w => w.FID == workItemId);
        if (workItem == null)
        {
            _logger.LogWarning("工作项 {WorkItemId} 不存在，跳过派发处理", workItemId);
            return new DispatchResult
            {
                Success = false,
                DispatchMode = "None",
                Message = $"工作项 {workItemId} 不存在"
            };
        }

        // 如果已经有处理人，直接返回成功
        if (workItem.FAssigneeId.HasValue)
        {
            return new DispatchResult
            {
                Success = true,
                AssigneeId = workItem.FAssigneeId,
                AssigneeName = workItem.FAssigneeName,
                DispatchMode = "PreAssigned",
                Message = "工作项已预分配处理人"
            };
        }

        // 查找匹配的派发规则
        var rule = await FindMatchingRuleAsync(workItem.FOrgId, workItem.FModule ?? "", workItem.FBizType ?? "");
        if (rule == null)
        {
            _logger.LogInformation("工作项 {WorkItemId} 未匹配到派发规则，进入待分配池", workItemId);
            return new DispatchResult
            {
                Success = false,
                DispatchMode = "Manual",
                Message = "未匹配到派发规则，进入待分配池"
            };
        }

        var mode = (DispatchMode)rule.FDispatchMode;
        switch (mode)
        {
            case Enums.DispatchMode.Auto:
                return await DispatchAutoAsync(workItem, rule);

            case Enums.DispatchMode.Hybrid:
                return new DispatchResult
                {
                    Success = true,
                    DispatchMode = "Hybrid",
                    Message = "已推荐候选人，等待人工确认"
                };

            case Enums.DispatchMode.Manual:
            default:
                return new DispatchResult
                {
                    Success = true,
                    DispatchMode = "Manual",
                    Message = "工作项已进入待分配池"
                };
        }
    }

    public async Task<WfDispatchRule?> FindMatchingRuleAsync(long orgId, string module, string bizType)
    {
        // 按优先级查找最高优先的启用规则：先精确匹配 Module + BizType，再退化匹配仅 Module
        var rule = await _db.Set<WfDispatchRule>()
            .Where(r => r.FOrgId == orgId && r.FIsEnabled
                && r.FModule == module && r.FBizType == bizType)
            .OrderByDescending(r => r.FPriority)
            .FirstOrDefaultAsync();

        if (rule != null) return rule;

        // 退化匹配仅 Module
        rule = await _db.Set<WfDispatchRule>()
            .Where(r => r.FOrgId == orgId && r.FIsEnabled
                && r.FModule == module && (r.FBizType == null || r.FBizType == ""))
            .OrderByDescending(r => r.FPriority)
            .FirstOrDefaultAsync();

        return rule;
    }

    public async Task ProcessTimeoutsAsync()
    {
        var now = DateTime.Now;

        // 查找所有超时未完成的工作项
        var expiredItems = await _db.Set<WfWorkItem>()
            .Where(w => w.FDeadline.HasValue
                && w.FDeadline < now
                && w.FStatus != (int)WorkItemStatus.Completed
                && w.FStatus != (int)WorkItemStatus.Cancelled
                && w.FStatus != (int)WorkItemStatus.Expired)
            .ToListAsync();

        foreach (var item in expiredItems)
        {
            var oldStatus = item.FStatus;
            item.FStatus = (int)WorkItemStatus.Expired;
            item.FUpdateTime = now;

            // 写日志
            _db.Set<WfWorkItemLog>().Add(new WfWorkItemLog
            {
                FWorkItemId = item.FID,
                FOperatorId = 0,
                FOperatorName = "System",
                FAction = "Expired",
                FFromStatus = oldStatus,
                FToStatus = (int)WorkItemStatus.Expired,
                FContent = "工作项已超时"
            });

            _logger.LogWarning("工作项 {WorkItemId} 已超时，标题: {Title}", item.FID, item.FTitle);
        }

        if (expiredItems.Count > 0)
            await _db.SaveChangesAsync();

        // 对有升级规则的超时工作项执行升级
        foreach (var item in expiredItems)
        {
            try
            {
                await EscalateAsync(item.FID);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "工作项 {WorkItemId} 升级处理失败", item.FID);
            }
        }

        _logger.LogInformation("超时检查完成，共处理 {Count} 个超时工作项", expiredItems.Count);
    }

    public async Task EscalateAsync(long workItemId)
    {
        var workItem = await _db.Set<WfWorkItem>().FirstOrDefaultAsync(w => w.FID == workItemId);
        if (workItem == null) return;

        // 查找关联的派发规则获取升级策略
        var rule = await FindMatchingRuleAsync(workItem.FOrgId, workItem.FModule ?? "", workItem.FBizType ?? "");
        if (rule?.FEscalationRule == null)
        {
            _logger.LogDebug("工作项 {WorkItemId} 无升级规则，跳过", workItemId);
            return;
        }

        var oldStatus = workItem.FStatus;

        // 重新分配：清除处理人，放回待分配池，提高优先级
        workItem.FAssigneeId = null;
        workItem.FAssigneeName = null;
        workItem.FStatus = (int)WorkItemStatus.Pending;
        workItem.FPriority = (int)WorkItemPriority.High;
        workItem.FUpdateTime = DateTime.Now;

        // 写日志
        _db.Set<WfWorkItemLog>().Add(new WfWorkItemLog
        {
            FWorkItemId = workItemId,
            FOperatorId = 0,
            FOperatorName = "System",
            FAction = "Escalated",
            FFromStatus = oldStatus,
            FToStatus = (int)WorkItemStatus.Pending,
            FContent = $"超时自动升级，重新进入待分配池（规则: {rule.FEscalationRule}）"
        });

        await _db.SaveChangesAsync();
        _logger.LogInformation("工作项 {WorkItemId} 已升级，重新进入待分配池", workItemId);
    }

    #region Private Helpers

    private async Task<DispatchResult> DispatchAutoAsync(WfWorkItem workItem, WfDispatchRule rule)
    {
        if (string.IsNullOrEmpty(rule.FAutoAssignRule))
        {
            return new DispatchResult
            {
                Success = false,
                DispatchMode = "Auto",
                Message = "自动分配规则为空，无法派发"
            };
        }

        try
        {
            // 解析 AutoAssignRule JSON，格式示例：{"assigneeId": 123, "assigneeName": "张三"}
            var ruleJson = JsonDocument.Parse(rule.FAutoAssignRule);
            var root = ruleJson.RootElement;

            long? assigneeId = null;
            string? assigneeName = null;

            if (root.TryGetProperty("assigneeId", out var aidElem))
                assigneeId = aidElem.GetInt64();
            if (root.TryGetProperty("assigneeName", out var anameElem))
                assigneeName = anameElem.GetString();

            if (!assigneeId.HasValue)
            {
                return new DispatchResult
                {
                    Success = false,
                    DispatchMode = "Auto",
                    Message = "自动分配规则未指定处理人"
                };
            }

            // 直接分配
            workItem.FAssigneeId = assigneeId.Value;
            workItem.FAssigneeName = assigneeName;
            workItem.FUpdateTime = DateTime.Now;

            _db.Set<WfWorkItemLog>().Add(new WfWorkItemLog
            {
                FWorkItemId = workItem.FID,
                FOperatorId = 0,
                FOperatorName = "DispatchEngine",
                FAction = "Assigned",
                FContent = $"自动派发给 {assigneeName}（规则: {rule.FName}）"
            });

            await _db.SaveChangesAsync();

            return new DispatchResult
            {
                Success = true,
                AssigneeId = assigneeId.Value,
                AssigneeName = assigneeName,
                DispatchMode = "Auto",
                Message = $"已自动派发给 {assigneeName}"
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "解析派发规则 JSON 失败，规则: {RuleName}", rule.FName);
            return new DispatchResult
            {
                Success = false,
                DispatchMode = "Auto",
                Message = $"派发规则 JSON 解析失败: {ex.Message}"
            };
        }
    }

    #endregion
}
