using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Interfaces;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.System.Entities;
using STOTOP.Module.Workflow.DTOs;
using STOTOP.Module.Workflow.Entities;
using STOTOP.Module.Workflow.Enums;
using STOTOP.Module.Workflow.Services.Interfaces;
using STOTOP.Module.CardFlow.Services.Interfaces;

namespace STOTOP.Module.CardFlow.Services;

/// <summary>
/// 质量问题派发服务 - 按错误类型分组，查询注册表派发配置，创建 WorkItem 并推送通知
/// </summary>
public class QualityDispatchService : IQualityDispatchService
{
    private readonly STOTOPDbContext _context;
    private readonly IWorkItemService _workItemService;
    private readonly IWorkHubNotifier _workHubNotifier;
    private readonly ILogger<QualityDispatchService> _logger;

    public QualityDispatchService(
        STOTOPDbContext context,
        IWorkItemService workItemService,
        IWorkHubNotifier workHubNotifier,
        ILogger<QualityDispatchService> logger)
    {
        _context = context;
        _workItemService = workItemService;
        _workHubNotifier = workHubNotifier;
        _logger = logger;
    }

    public async Task DispatchAsync(long batchId, long orgId, IReadOnlyList<CfBatchError> errors)
    {
        if (errors == null || errors.Count == 0)
            return;

        var grouped = errors.GroupBy(e => e.FErrorType);

        foreach (var group in grouped)
        {
            try
            {
                await DispatchGroupAsync(batchId, orgId, group.Key, group.ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "质量问题派发异常：批次 {BatchId}，错误类型 {ErrorType}", batchId, group.Key);
            }
        }
    }

    private async Task DispatchGroupAsync(long batchId, long orgId, string errorType, List<CfBatchError> groupErrors)
    {
        // 1. 查询注册表获取派发配置
        var issueType = await _context.Set<CfQualityIssueType>()
            .FirstOrDefaultAsync(t => t.FCode == errorType && t.FStatus == 1);

        if (issueType == null)
        {
            _logger.LogWarning("质量问题类型 {Code} 未在注册表中找到或已禁用，跳过派发", errorType);
            return;
        }

        var resolveMode = NormalizeResolveMode(issueType.FResolveMode);

        // 2. 新策略为空时，回退旧派发模式；新策略为 None 时只记录不派发
        if (resolveMode == null && (string.IsNullOrEmpty(issueType.FDispatchMode) || issueType.FDispatchMode == "None"))
        {
            if (string.IsNullOrEmpty(issueType.FDispatchMode))
                _logger.LogWarning("质量问题类型 {Code} 未配置派发模式（FDispatchMode 为空），跳过派发", errorType);
            return;
        }

        if (resolveMode == "None")
        {
            await MarkErrorsDispatchedAsync(groupErrors, "None", "None", null, errorType);
            _logger.LogInformation("质量问题类型 {Code} 配置为 None，仅记录不派发，批次 {BatchId}", errorType, batchId);
            return;
        }

        // 3. 去重检查：同一批次 + 同一类型是否已有活跃 WorkItem
        var hasActiveWorkItem = await _context.Set<WfWorkItem>()
            .AnyAsync(w => w.FModule == "DataCenter"
                && w.FBizType == errorType
                && w.FBizId == batchId
                && w.FOrgId == orgId
                && w.FStatus != (int)WorkItemStatus.Completed
                && w.FStatus != (int)WorkItemStatus.Cancelled);

        if (hasActiveWorkItem)
        {
            _logger.LogInformation("质量问题类型 {Code} 在批次 {BatchId} 已有活跃 WorkItem，跳过重复派发",
                errorType, batchId);
            return;
        }

        // 4. 创建 WorkItem。InlineCard/GuideToPage/DedicatedFlow 第一版先创建统一入口，
        // 后续由前端根据 ResolveMode 渲染卡片或跳转专用页面。
        var effectiveMode = resolveMode ?? "WorkItem";
        var detailRoute = BuildDetailRoute(issueType, batchId, errorType, effectiveMode);
        var request = new CreateWorkItemRequest
        {
            OrgId = orgId,
            Title = BuildTitle(issueType, groupErrors.Count, batchId, effectiveMode),
            Description = BuildDescription(issueType, effectiveMode),
            Type = (int)WorkItemType.Task,
            Source = (int)WorkItemSource.Pipeline,
            Priority = issueType.FSeverityLevel == "Error" ? (int)WorkItemPriority.High : (int)WorkItemPriority.Normal,
            Module = "DataCenter",
            BizType = errorType,
            BizId = batchId,
            DetailRoute = detailRoute,
            CreatorId = 0, // 系统自动创建
            Deadline = issueType.FTimeoutHours > 0 ? DateTime.Now.AddHours(issueType.FTimeoutHours) : null
        };

        WorkItemDto? workItem;
        try
        {
            workItem = await _workItemService.CreateAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建 WorkItem 失败：批次 {BatchId}，类型 {ErrorType}", batchId, errorType);
            return;
        }

        _logger.LogInformation("质量问题派发成功：批次 {BatchId}，类型 {ErrorType}，WorkItem {WorkItemId}",
            batchId, errorType, workItem.Id);

        await MarkErrorsDispatchedAsync(groupErrors, effectiveMode, "Dispatched", workItem.Id, errorType);

        // 5. 根据旧派发目标配置推送通知；新策略可以只创建入口、不推送
        if (!string.IsNullOrEmpty(issueType.FDispatchMode) && issueType.FDispatchMode != "None")
            await NotifyByDispatchModeAsync(issueType, orgId, workItem);
    }

    private async Task MarkErrorsDispatchedAsync(
        List<CfBatchError> groupErrors,
        string dispatchType,
        string dispatchStatus,
        long? workItemId,
        string errorType)
    {
        var ids = groupErrors.Select(e => e.FID).ToList();
        if (ids.Count == 0) return;

        var records = await _context.Set<CfBatchError>()
            .Where(e => ids.Contains(e.FID))
            .ToListAsync();
        foreach (var record in records)
        {
            record.FDispatchType = dispatchType;
            record.FDispatchStatus = dispatchStatus;
            record.FWorkItemId = workItemId ?? record.FWorkItemId;
            record.FIssueType = string.IsNullOrWhiteSpace(record.FIssueType) ? errorType : record.FIssueType;
            if (string.IsNullOrWhiteSpace(record.FResolutionStatus))
                record.FResolutionStatus = "Pending";
        }

        await _context.SaveChangesAsync();
    }

    private static string? NormalizeResolveMode(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode)) return null;
        return mode.Trim() switch
        {
            "None" => "None",
            "WorkItem" => "WorkItem",
            "InlineCard" => "InlineCard",
            "GuideToPage" => "GuideToPage",
            "DedicatedFlow" => "DedicatedFlow",
            _ => "WorkItem"
        };
    }

    private static string BuildTitle(CfQualityIssueType issueType, int count, long batchId, string resolveMode)
    {
        var prefix = resolveMode switch
        {
            "InlineCard" => "待卡片处理",
            "GuideToPage" => "待功能页处理",
            "DedicatedFlow" => "待流程处理",
            _ => "质量问题待处理"
        };
        return $"{prefix}: {issueType.FName}({count}条) - 批次{batchId}";
    }

    private static string BuildDescription(CfQualityIssueType issueType, string resolveMode)
    {
        var modeText = resolveMode switch
        {
            "InlineCard" => "可在异常处理卡片中直接处理",
            "GuideToPage" => "需跳转到指定业务页面处理",
            "DedicatedFlow" => "需启动或进入专用卡片流程处理",
            _ => "按工作项处理"
        };
        return $"{modeText}：{issueType.FDescription ?? issueType.FName}";
    }

    private static string BuildDetailRoute(CfQualityIssueType issueType, long batchId, string errorType, string resolveMode)
    {
        var baseRoute = resolveMode == "GuideToPage" && !string.IsNullOrWhiteSpace(issueType.FDetailRoute)
            ? issueType.FDetailRoute!
            : "/express/quality-center/dashboard";

        var separator = baseRoute.Contains('?') ? "&" : "?";
        var route = $"{baseRoute}{separator}batchId={batchId}&issueType={Uri.EscapeDataString(errorType)}&resolveMode={Uri.EscapeDataString(resolveMode)}";

        if (resolveMode == "DedicatedFlow" && !string.IsNullOrWhiteSpace(issueType.FCardFlowCode))
            route += $"&flowCode={Uri.EscapeDataString(issueType.FCardFlowCode)}";
        if (!string.IsNullOrWhiteSpace(issueType.FCardTemplateCode))
            route += $"&templateCode={Uri.EscapeDataString(issueType.FCardTemplateCode)}";

        return route;
    }

    private async Task NotifyByDispatchModeAsync(CfQualityIssueType issueType, long orgId, WorkItemDto workItem)
    {
        var config = ParseDispatchTarget(issueType.FDispatchTarget);

        if (config == null)
        {
            _logger.LogWarning("类型 {Code} 派发模式为 {Mode} 但目标配置为空或格式错误，WorkItem 已创建但无法推送通知",
                issueType.FCode, issueType.FDispatchMode);
            return;
        }

        // 转换为 SignalR 推送用的 Core.Models.WorkItemDto
        var pushDto = new STOTOP.Core.Models.WorkItemDto
        {
            Id = workItem.Id,
            Source = "quality",
            Category = "todo",
            Title = workItem.Title,
            Summary = workItem.Description ?? "",
            Priority = workItem.Priority,
            RelatedUrl = workItem.DetailRoute,
            CreatedAt = workItem.CreateTime,
            Metadata = new Dictionary<string, object>
            {
                ["bizType"] = workItem.BizType ?? "",
                ["bizId"] = workItem.BizId ?? 0,
                ["module"] = workItem.Module ?? ""
            }
        };

        switch (issueType.FDispatchMode)
        {
            case "Role":
                await DispatchToRoleAsync(config, issueType, orgId, workItem, pushDto);
                break;

            case "Assignee":
                await DispatchToAssigneeAsync(config, workItem, pushDto);
                break;

            default:
                _logger.LogWarning("未知的派发模式 {Mode}，类型 {Code}", issueType.FDispatchMode, issueType.FCode);
                break;
        }
    }

    private async Task DispatchToRoleAsync(
        DispatchTargetConfig config,
        CfQualityIssueType issueType,
        long orgId,
        WorkItemDto workItem,
        STOTOP.Core.Models.WorkItemDto pushDto)
    {
        if (string.IsNullOrEmpty(config.RoleCode))
        {
            _logger.LogWarning("类型 {Code} 配置为 Role 模式但 RoleCode 为空", issueType.FCode);
            return;
        }

        // 查找角色ID
        var role = await _context.Set<SysRole>()
            .FirstOrDefaultAsync(r => r.FCode == config.RoleCode);

        if (role == null)
        {
            _logger.LogWarning("类型 {Code} 配置的角色 {RoleCode} 不存在", issueType.FCode, config.RoleCode);
            return;
        }

        // 查找角色下的用户（考虑组织作用域）
        var userIdsQuery = _context.Set<SysUserRole>()
            .Where(ur => ur.FRoleId == role.FID);

        if (issueType.FOrgScoped)
        {
            userIdsQuery = userIdsQuery.Where(ur => ur.FOrgId == orgId || ur.FOrgId == null);
        }

        var userIds = await userIdsQuery
            .Select(ur => ur.FUserId)
            .Distinct()
            .ToListAsync();

        if (userIds.Count == 0)
        {
            _logger.LogWarning("角色 {RoleCode} 在组织 {OrgId} 下无用户，WorkItem {WorkItemId} 已创建但无法推送通知",
                config.RoleCode, orgId, workItem.Id);
            return;
        }

        // 逐一推送通知
        foreach (var userId in userIds)
        {
            try
            {
                await _workHubNotifier.AddWorkItemAsync(userId, pushDto);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "向用户 {UserId} 推送 WorkItem {WorkItemId} 通知失败", userId, workItem.Id);
            }
        }

        _logger.LogInformation("质量问题 WorkItem {WorkItemId} 已推送给角色 {RoleCode} 下 {Count} 个用户",
            workItem.Id, config.RoleCode, userIds.Count);
    }

    private async Task DispatchToAssigneeAsync(
        DispatchTargetConfig config,
        WorkItemDto workItem,
        STOTOP.Core.Models.WorkItemDto pushDto)
    {
        if (!config.AssigneeId.HasValue || config.AssigneeId.Value <= 0)
        {
            _logger.LogWarning("Assignee 模式但 AssigneeId 为空，WorkItem {WorkItemId} 已创建但未指派", workItem.Id);
            return;
        }

        // 指派给具体人
        try
        {
            await _workItemService.AssignAsync(workItem.Id, config.AssigneeId.Value, config.AssigneeName ?? "");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "指派 WorkItem {WorkItemId} 给用户 {AssigneeId} 失败",
                workItem.Id, config.AssigneeId.Value);
        }

        // 推送通知
        try
        {
            await _workHubNotifier.AddWorkItemAsync(config.AssigneeId.Value, pushDto);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "向用户 {UserId} 推送 WorkItem {WorkItemId} 通知失败",
                config.AssigneeId.Value, workItem.Id);
        }

        _logger.LogInformation("质量问题 WorkItem {WorkItemId} 已指派给用户 {AssigneeId}",
            workItem.Id, config.AssigneeId.Value);
    }

    private static DispatchTargetConfig? ParseDispatchTarget(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<DispatchTargetConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return null;
        }
    }

    /// <summary>派发目标配置</summary>
    private class DispatchTargetConfig
    {
        public string? RoleCode { get; set; }
        public long? AssigneeId { get; set; }
        public string? AssigneeName { get; set; }
    }
}
