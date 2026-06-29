using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Module.CardFlow.Services.Interfaces;
using STOTOP.Module.Contract.Services.Interfaces;
using STOTOP.Module.Finance.Services.Interfaces;
using STOTOP.Module.Points.Services;
using STOTOP.Module.Quality.Services.Exception;
using STOTOP.Module.Task.Services;
using STOTOP.Module.Workflow.Enums;
using STOTOP.Module.Workflow.Services.Interfaces;
using STOTOP.WebAPI.Dtos.WorkHub;
// 消歧义：本文件 IVoucherService 指 Finance 内部接口
using IVoucherService = STOTOP.Module.Finance.Services.Interfaces.IVoucherService;
// 明确引用 Dtos 层的 WorkItemDto，与 STOTOP.Core.Models.WorkItemDto 区分
using DtoWorkItemDto = STOTOP.WebAPI.Dtos.WorkHub.WorkItemDto;
using WfWorkItemDto = STOTOP.Module.Workflow.DTOs.WorkItemDto;

namespace STOTOP.WebAPI.Services;

public interface IWorkHubService
{
    Task<PagedResult<DtoWorkItemDto>> GetWorkItemsAsync(long userId, long orgId, string? category, string? priority, int page, int pageSize);
    Task<WorkHubStatsDto> GetStatsAsync(long userId, long orgId);
    Task<WorkItemsWithStatsDto> GetWorkItemsWithStatsAsync(long userId, long orgId, string? priority, int page, int pageSize);
    Task<bool> ExecuteActionAsync(long userId, string itemId, string actionKey);
}

public class WorkHubService : IWorkHubService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IWorkHubNotifier _workHubNotifier;
    private readonly ILogger<WorkHubService> _logger;

    // 缓存当前 scope 内的服务实例（用于非并行场景如 ExecuteActionAsync）
    private readonly IExceptionService _exceptionService;
    private readonly ITaskService _taskService;
    private readonly INotificationService _notificationService;
    private readonly IContractReminderService _contractReminderService;
    private readonly IPointApplicationService _pointApplicationService;
    private readonly IVoucherService? _voucherService;

    // 优先级排序权重（数值越小越优先�?
    private static readonly Dictionary<string, int> PriorityOrder = new()
    {
        ["urgent"] = 0,
        ["high"] = 1,
        ["normal"] = 2,
        ["low"] = 3,
    };

    public WorkHubService(
        IServiceScopeFactory scopeFactory,
        IExceptionService exceptionService,
        ITaskService taskService,
        INotificationService notificationService,
        IContractReminderService contractReminderService,
        IPointApplicationService pointApplicationService,
        IWorkHubNotifier workHubNotifier,
        ILogger<WorkHubService> logger,
        IVoucherService? voucherService = null)
    {
        _scopeFactory = scopeFactory;
        _exceptionService = exceptionService;
        _taskService = taskService;
        _notificationService = notificationService;
        _contractReminderService = contractReminderService;
        _pointApplicationService = pointApplicationService;
        _workHubNotifier = workHubNotifier;
        _voucherService = voucherService;
        _logger = logger;
    }

    // ===== WfWorkItem 数据源（WF 框架） =====
    private async Task<List<DtoWorkItemDto>> GetWfWorkItemsFromServiceAsync(IWorkItemService workItemService, long userId)
    {
        var wfItems = await workItemService.GetPendingItemsAsync(userId);
        return wfItems.Select(MapWfWorkItemToDto).ToList();
    }

    /// <summary>
    /// 将 WF 模块的 WorkItemDto 映射为 WorkHub 的 DtoWorkItemDto
    /// </summary>
    private static DtoWorkItemDto MapWfWorkItemToDto(WfWorkItemDto wf)
    {
        var source = MapWfModule(wf.Module);
        var category = MapWfType(wf.Type);
        var priority = MapWfPriority(wf.Priority);

        return new DtoWorkItemDto
        {
            Id = $"wf-{wf.Id}",
            Source = source,
            Category = category,
            Priority = priority,
            Title = wf.Title,
            Summary = wf.Description ?? $"{wf.TypeText} - {wf.StatusText}",
            Timestamp = wf.CreateTime,
            Deadline = wf.Deadline,
            Actions = new List<WorkItemActionDto>
            {
                new() { Key = "view", Label = "查看详情", Type = "primary", Route = wf.DetailRoute }
            },
            DetailRoute = wf.DetailRoute,
            Metadata = new Dictionary<string, object>
            {
                ["wfId"] = wf.Id,
                ["wfUid"] = wf.Uid,
                ["chainId"] = wf.ChainId ?? "",
                ["module"] = wf.Module ?? "",
                ["bizType"] = wf.BizType ?? "",
                ["bizId"] = wf.BizId ?? 0L,
                ["assigneeName"] = wf.AssigneeName ?? "",
            }
        };
    }

    private static string MapWfModule(string? module) => module?.ToLower() switch
    {
        "datacenter" => "datacenter",
        "finance" => "finance",
        "quality" => "quality",
        "contract" => "contract",
        "points" => "points",
        _ => "workflow",
    };

    private static string MapWfType(int type) => type switch
    {
        (int)WorkItemType.Approval => "approval",
        (int)WorkItemType.Alert => "alert",
        (int)WorkItemType.Reminder => "reminder",
        _ => "task",
    };

    private static string MapWfPriority(int priority) => priority switch
    {
        (int)WorkItemPriority.Urgent => "urgent",
        (int)WorkItemPriority.High => "high",
        (int)WorkItemPriority.Low => "low",
        _ => "normal",
    };

    /// <summary>
    /// 由统一 DTO 派生「业务类型」（用户可见标签 + 上色键），不依赖 source 框架黑话。
    /// 纯函数，便于单测。
    /// </summary>
    internal static (string Key, string Label) ResolveBizType(DtoWorkItemDto item)
    {
        string? Meta(string k) =>
            item.Metadata != null && item.Metadata.TryGetValue(k, out var v) ? v?.ToString() : null;

        switch (item.Source)
        {
            case "cardflow":
                var flow = Meta("flowName");
                return string.IsNullOrWhiteSpace(flow) ? ("approval", "审批") : ("flow:" + flow, flow!);
            case "finance":
                return ("voucher", "凭证复核");
            case "quality":
                return ("quality", "质量异常");
            case "contract":
                return ("contract", "合同到期");
            case "points":
                return ("points", "积分审批");
            case "task":
                return item.Category == "notification" ? ("notification", "通知") : ("task", "任务");
            default:
                var biz = Meta("bizType");
                return string.IsNullOrWhiteSpace(biz) ? ("approval", "审批") : ("wf:" + biz, biz!);
        }
    }

    public async Task<PagedResult<DtoWorkItemDto>> GetWorkItemsAsync(long userId, long orgId, string? category, string? priority, int page, int pageSize)
    {
        // 并行获取各模块数据（每个查询在独立 scope 中执行，避免 DbContext 并发问题）
        var tasks = new List<Task<List<DtoWorkItemDto>>>();

        if (string.IsNullOrEmpty(category) || category == "approval")
        {
            tasks.Add(ExecuteInScopeAsync<ITodoService>(svc => GetCardFlowTodosFromServiceAsync(svc, userId)));
            tasks.Add(ExecuteInScopeAsync<IPointApplicationService>(svc => GetPointsApprovalsFromServiceAsync(svc, orgId)));
            tasks.Add(ExecuteInScopeOptionalAsync<IVoucherService>(svc => GetFinanceApprovalsFromServiceAsync(svc)));
        }

        if (string.IsNullOrEmpty(category) || category == "task")
        {
            tasks.Add(ExecuteInScopeAsync<ITaskService>(svc => GetTaskItemsFromServiceAsync(svc, orgId, userId)));
        }

        if (string.IsNullOrEmpty(category) || category == "alert")
        {
            tasks.Add(ExecuteInScopeAsync<IExceptionService>(svc => GetQualityAlertsFromServiceAsync(svc, orgId)));
        }

        if (string.IsNullOrEmpty(category) || category == "notification")
        {
            tasks.Add(ExecuteInScopeAsync<INotificationService>(svc => GetNotificationsFromServiceAsync(svc, userId)));
        }

        if (string.IsNullOrEmpty(category) || category == "reminder")
        {
            tasks.Add(ExecuteInScopeAsync<IContractReminderService>(svc => GetContractRemindersFromServiceAsync(svc, userId)));
        }

        // WfWorkItem 作为主数据源（涵盖所有类别）
        tasks.Add(ExecuteInScopeAsync<IWorkItemService>(svc => GetWfWorkItemsFromServiceAsync(svc, userId)));

        // 并行执行所有查询
        var results = await Task.WhenAll(tasks);
        var allItems = results.SelectMany(r => r).ToList();

        // 回填业务类型（BizTypeKey / BizTypeLabel）
        EnrichBizTypes(allItems);

        // 按优先级筛�?
        if (!string.IsNullOrEmpty(priority))
        {
            allItems = allItems.Where(i => i.Priority == priority).ToList();
        }

        // 排序：优先级 �?时间戳（最新优先）
        allItems = allItems
            .OrderBy(i => PriorityOrder.GetValueOrDefault(i.Priority, 99))
            .ThenByDescending(i => i.Timestamp)
            .ToList();

        var total = allItems.Count;
        var pageItems = allItems
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        // 填充 relatedLinks 与扩展 action 字段
        foreach (var item in pageItems)
        {
            EnrichWorkItem(item);
        }

        return new PagedResult<DtoWorkItemDto>
        {
            Items = pageItems,
            Total = total,
            PageIndex = page,
            PageSize = pageSize,
        };
    }

    public async Task<WorkItemsWithStatsDto> GetWorkItemsWithStatsAsync(long userId, long orgId, string? priority, int page, int pageSize)
    {
        // 并行获取所有模块数据（只执行一次，同时用于 items 和 stats）
        var tasks = new List<Task<List<DtoWorkItemDto>>>
        {
            ExecuteInScopeAsync<ITodoService>(svc => GetCardFlowTodosFromServiceAsync(svc, userId)),
            ExecuteInScopeAsync<IPointApplicationService>(svc => GetPointsApprovalsFromServiceAsync(svc, orgId)),
            ExecuteInScopeOptionalAsync<IVoucherService>(svc => GetFinanceApprovalsFromServiceAsync(svc)),
            ExecuteInScopeAsync<ITaskService>(svc => GetTaskItemsFromServiceAsync(svc, orgId, userId)),
            ExecuteInScopeAsync<IExceptionService>(svc => GetQualityAlertsFromServiceAsync(svc, orgId)),
            ExecuteInScopeAsync<INotificationService>(svc => GetNotificationsFromServiceAsync(svc, userId)),
            ExecuteInScopeAsync<IContractReminderService>(svc => GetContractRemindersFromServiceAsync(svc, userId)),
            // WfWorkItem 主数据源
            ExecuteInScopeAsync<IWorkItemService>(svc => GetWfWorkItemsFromServiceAsync(svc, userId)),
        };

        var results = await Task.WhenAll(tasks);
        var allItems = results.SelectMany(r => r).ToList();

        // 回填业务类型（BizTypeKey / BizTypeLabel）
        EnrichBizTypes(allItems);

        // 计算统计（基于全量数据）
        var statsResult = new WorkHubStatsDto
        {
            Total = allItems.Count,
            Approval = allItems.Count(i => i.Category == "approval"),
            Task = allItems.Count(i => i.Category == "task"),
            Alert = allItems.Count(i => i.Category == "alert"),
            Notification = allItems.Count(i => i.Category == "notification"),
            Reminder = allItems.Count(i => i.Category == "reminder"),
            Initiated = allItems.Count(i => i.Category == "initiated"),
        };

        // 按优先级筛选
        if (!string.IsNullOrEmpty(priority))
        {
            allItems = allItems.Where(i => i.Priority == priority).ToList();
        }

        // 排序：优先级 → 时间戳（最新优先）
        allItems = allItems
            .OrderBy(i => PriorityOrder.GetValueOrDefault(i.Priority, 99))
            .ThenByDescending(i => i.Timestamp)
            .ToList();

        var total = allItems.Count;
        var pageItems = allItems
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        // 填充 relatedLinks 与扩展 action 字段
        foreach (var item in pageItems)
        {
            EnrichWorkItem(item);
        }

        return new WorkItemsWithStatsDto
        {
            Items = new PagedResult<DtoWorkItemDto>
            {
                Items = pageItems,
                Total = total,
                PageIndex = page,
                PageSize = pageSize,
            },
            Stats = statsResult,
        };
    }

    public async Task<WorkHubStatsDto> GetStatsAsync(long userId, long orgId)
    {
        // 并行获取各模块数据（每个查询在独立 scope 中执行，避免 DbContext 并发问题）
        var tasks = new List<Task<List<DtoWorkItemDto>>>
        {
            ExecuteInScopeAsync<ITodoService>(svc => GetCardFlowTodosFromServiceAsync(svc, userId)),
            ExecuteInScopeAsync<IPointApplicationService>(svc => GetPointsApprovalsFromServiceAsync(svc, orgId)),
            ExecuteInScopeOptionalAsync<IVoucherService>(svc => GetFinanceApprovalsFromServiceAsync(svc)),
            ExecuteInScopeAsync<ITaskService>(svc => GetTaskItemsFromServiceAsync(svc, orgId, userId)),
            ExecuteInScopeAsync<IExceptionService>(svc => GetQualityAlertsFromServiceAsync(svc, orgId)),
            ExecuteInScopeAsync<INotificationService>(svc => GetNotificationsFromServiceAsync(svc, userId)),
            ExecuteInScopeAsync<IContractReminderService>(svc => GetContractRemindersFromServiceAsync(svc, userId)),
            // WfWorkItem 主数据源
            ExecuteInScopeAsync<IWorkItemService>(svc => GetWfWorkItemsFromServiceAsync(svc, userId)),
        };

        // 并行执行所有查询
        var results = await Task.WhenAll(tasks);
        var allItems = results.SelectMany(r => r).ToList();

        return new WorkHubStatsDto
        {
            Total = allItems.Count,
            Approval = allItems.Count(i => i.Category == "approval"),
            Task = allItems.Count(i => i.Category == "task"),
            Alert = allItems.Count(i => i.Category == "alert"),
            Notification = allItems.Count(i => i.Category == "notification"),
            Reminder = allItems.Count(i => i.Category == "reminder"),
            Initiated = allItems.Count(i => i.Category == "initiated"),
        };
    }

    /// <summary>
    /// 在独立 scope 中执行查询，仅解析实际需要的服务
    /// </summary>
    private async Task<List<DtoWorkItemDto>> ExecuteInScopeAsync<TService>(
        Func<TService, Task<List<DtoWorkItemDto>>> query) where TService : notnull
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<TService>();
            return await query(service);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "并行查询执行失败");
            return new List<DtoWorkItemDto>();
        }
    }

    /// <summary>
    /// 在独立 scope 中执行查询（可选服务版本），服务不存在时传入 null
    /// </summary>
    private async Task<List<DtoWorkItemDto>> ExecuteInScopeOptionalAsync<TService>(
        Func<TService?, Task<List<DtoWorkItemDto>>> query) where TService : class
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetService<TService>();
            return await query(service);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "并行查询执行失败");
            return new List<DtoWorkItemDto>();
        }
    }

    // ===== 质量异常告警 =====
    private static async Task<List<DtoWorkItemDto>> GetQualityAlertsFromServiceAsync(IExceptionService exceptionService, long orgId)
    {
        // 查询未关闭（状态非 3）的异常
        var request = new STOTOP.Module.Quality.Dtos.ExceptionPagedRequest
        {
            PageIndex = 1,
            PageSize = 50,
        };
        var result = await exceptionService.GetPagedAsync(orgId, request);
        if (result.Data == null) return new List<DtoWorkItemDto>();

        // 只取未关闭的
        var openItems = result.Data.Items.Where(e => e.Status < 3).ToList();

        return openItems.Select(e => new DtoWorkItemDto
        {
            Id = $"quality-{e.Id}",
            Source = "quality",
            Category = "alert",
            Priority = MapQualityPriority(e.Priority),
            Title = $"【质量异常】{e.Title}",
            Summary = $"异常编号：{e.ExceptionNo}，类型：{e.TypeText}，状态：{e.StatusText}" + (e.AssigneeName != null ? $"，负责人：{e.AssigneeName}" : ""),
            Timestamp = e.CreateTime,
            Deadline = e.Deadline,
            Actions = new List<WorkItemActionDto>
            {
                new() { Key = "view", Label = "查看详情", Type = "primary", Route = $"/quality/exceptions/{e.Id}" }
            },
            DetailRoute = $"/quality/exceptions/{e.Id}",
            Metadata = new Dictionary<string, object>
            {
                ["exceptionNo"] = e.ExceptionNo,
                ["type"] = e.Type,
                ["status"] = e.Status,
            }
        }).ToList();
    }

    // ===== 任务待办 =====
    private static async Task<List<DtoWorkItemDto>> GetTaskItemsFromServiceAsync(ITaskService taskService, long orgId, long userId)
    {
        var result = await taskService.GetMyTasksAsync(orgId, userId);
        if (result.Data == null) return new List<DtoWorkItemDto>();

        // 只取未完成的任务（status < 3）
        var pendingTasks = result.Data.Items.Where(t => t.Status < 3).ToList();

        return pendingTasks.Select(t => new DtoWorkItemDto
        {
            Id = $"task-{t.Id}",
            Source = "task",
            Category = "task",
            Priority = MapTaskPriority(t.Priority),
            Title = t.Title,
            Summary = $"任务状态：{MapTaskStatusText(t.Status)}" + (t.ProjectName != null ? $"，所属项目：{t.ProjectName}" : "") + (t.AssigneeName != null ? $"，负责人：{t.AssigneeName}" : ""),
            Timestamp = t.CreateTime,
            Deadline = t.PlanEnd,
            Actions = new List<WorkItemActionDto>
            {
                new() { Key = "view", Label = "查看任务", Type = "primary", Route = $"/task/tasks/{t.Id}" }
            },
            DetailRoute = $"/task/tasks/{t.Id}",
            Metadata = new Dictionary<string, object>
            {
                ["taskId"] = t.Id,
                ["status"] = t.Status,
                ["progress"] = t.Progress,
            }
        }).ToList();
    }

    // ===== 通知消息 =====
    private static async Task<List<DtoWorkItemDto>> GetNotificationsFromServiceAsync(INotificationService notificationService, long userId)
    {
        var request = new STOTOP.Module.Task.Dtos.NotificationPagedRequest
        {
            IsRead = false,
            PageIndex = 1,
            PageSize = 50,
        };
        var result = await notificationService.GetPagedListAsync(request, userId);
        if (result.Data == null) return new List<DtoWorkItemDto>();

        return result.Data.Items.Select(n => new DtoWorkItemDto
        {
            Id = $"notification-{n.Id}",
            Source = "task",
            Category = "notification",
            Priority = "normal",
            Title = n.Title,
            Summary = n.Content,
            Timestamp = n.CreateTime,
            Actions = new List<WorkItemActionDto>
            {
                new() { Key = "view", Label = "查看", Type = "default", Route = "/task/notifications" }
            },
            DetailRoute = "/task/notifications",
            Metadata = new Dictionary<string, object>
            {
                ["eventType"] = n.EventType,
                ["relationType"] = n.RelationType,
                ["relationId"] = n.RelationId,
            }
        }).ToList();
    }

    // ===== 合同到期提醒 =====
    private static async Task<List<DtoWorkItemDto>> GetContractRemindersFromServiceAsync(IContractReminderService contractReminderService, long userId)
    {
        var reminders = await contractReminderService.GetPendingRemindersAsync(userId);

        return reminders.Select(r => new DtoWorkItemDto
        {
            Id = $"contract-{r.Id}",
            Source = "contract",
            Category = "reminder",
            Priority = r.ReminderDate <= DateTime.Today ? "high" : "normal",
            Title = $"【合同提醒】{r.ContractTitle ?? r.ContractNo ?? "合同"}",
            Summary = $"合同编号：{r.ContractNo}，提醒日期：{r.ReminderDate:yyyy-MM-dd}" + (r.Remark != null ? $"，备注：{r.Remark}" : ""),
            Timestamp = r.CreatedTime,
            Deadline = r.ReminderDate,
            Actions = new List<WorkItemActionDto>
            {
                new() { Key = "view", Label = "查看合同", Type = "primary", Route = $"/contract/contracts/{r.ContractId}" }
            },
            DetailRoute = $"/contract/contracts/{r.ContractId}",
            Metadata = new Dictionary<string, object>
            {
                ["contractId"] = r.ContractId,
                ["reminderType"] = r.ReminderType,
                ["reminderDate"] = r.ReminderDate,
            }
        }).ToList();
    }

    // ===== 积分申请待审批 =====
    private static async Task<List<DtoWorkItemDto>> GetCardFlowTodosFromServiceAsync(ITodoService todoService, long userId)
    {
        var result = await todoService.GetMyTodosAsync(userId, new STOTOP.Module.CardFlow.Dtos.TodoQueryRequest
        {
            Page = 1,
            PageSize = 50,
            Status = "pending"
        });

        return result.Items.Select(todo => new DtoWorkItemDto
        {
            Id = $"cardflow-{todo.Id}",
            Source = "cardflow",
            Category = "approval",
            Priority = MapCardFlowPriority(todo.Priority),
            Title = string.IsNullOrWhiteSpace(todo.Title)
                ? $"【{todo.FlowName}】{todo.CardNumber ?? "待审批卡片"}"
                : todo.Title!,
            Summary = $"{todo.FlowName} · {todo.InitiatorName} · {todo.CardNumber ?? $"卡片#{todo.CardId}"}",
            Timestamp = todo.CreatedTime,
            Actions = new List<WorkItemActionDto>
            {
                new()
                {
                    Key = "view",
                    Label = "进入处理",
                    Type = "primary",
                    Route = $"/cardflow/cards/{todo.CardId}"
                }
            },
            DetailRoute = $"/cardflow/cards/{todo.CardId}",
            Metadata = new Dictionary<string, object>
            {
                ["cardflowTodoId"] = todo.Id,
                ["cardId"] = todo.CardId,
                ["cardNumber"] = todo.CardNumber ?? string.Empty,
                ["flowName"] = todo.FlowName,
                ["todoType"] = todo.Type,
                ["runtimeViewRoute"] = $"/cardflow/cards/{todo.CardId}",
            }
        }).ToList();
    }

    private static async Task<List<DtoWorkItemDto>> GetPointsApprovalsFromServiceAsync(IPointApplicationService pointApplicationService, long orgId)
    {
        var request = new STOTOP.Module.Points.Dtos.PendingApplicationPagedRequest
        {
            PageIndex = 1,
            PageSize = 50,
        };
        var result = await pointApplicationService.GetPendingAsync(orgId, request);
        if (result.Data == null) return new List<DtoWorkItemDto>();

        return result.Data.Items.Select(p => new DtoWorkItemDto
        {
            Id = $"points-{p.Id}",
            Source = "points",
            Category = "approval",
            Priority = "normal",
            Title = $"【积分申请】{p.ApplicantName} 申请 {p.PointValue} 积分",
            Summary = $"规则：{p.RuleName}，说明：{p.ApplicationNote}",
            Timestamp = p.CreateTime,
            Actions = new List<WorkItemActionDto>
            {
                new() { Key = "approve", Label = "审批", Type = "primary", Route = "/points/applications" },
            },
            DetailRoute = "/points/applications",
            Metadata = new Dictionary<string, object>
            {
                ["applicationId"] = p.Id,
                ["applicantId"] = p.ApplicantId,
                ["pointValue"] = p.PointValue,
            }
        }).ToList();
    }

    // ===== 财务凭证待审批 =====
    private static async Task<List<DtoWorkItemDto>> GetFinanceApprovalsFromServiceAsync(IVoucherService? voucherService)
    {
        if (voucherService == null)
            return new List<DtoWorkItemDto>();

        var count = await voucherService.GetPendingAuditCountAsync();
        if (count <= 0)
            return new List<DtoWorkItemDto>();

        // 汇总展示一条通知
        return new List<DtoWorkItemDto>
        {
            new DtoWorkItemDto
            {
                Id = "finance-pending-audit",
                Source = "finance",
                Category = "approval",
                Priority = "normal",
                Title = $"【财务】有 {count} 张凭证待审核",
                Summary = $"共 {count} 张凭证等待审核处理",
                Timestamp = DateTime.Now,
                Actions = new List<WorkItemActionDto>
                {
                    new() { Key = "view", Label = "前往审核", Type = "primary", Route = "/finance/voucher/list" }
                },
                DetailRoute = "/finance/voucher/list",
                Metadata = new Dictionary<string, object>
                {
                    ["pendingCount"] = count,
                }
            }
        };
    }

    // ===== 内联操作 =====

    public async Task<bool> ExecuteActionAsync(long userId, string itemId, string actionKey)
    {
        var parts = itemId.Split('-', 2);
        if (parts.Length < 2)
            throw new ArgumentException($"无效的工作项ID格式: {itemId}");

        var source = parts[0];

        switch (source)
        {
            case "points":
                _logger.LogInformation("积分审批操作：itemId={ItemId}, action={Action}, userId={UserId}", itemId, actionKey, userId);
                // 后续任务中接入真实积分审批逻辑
                break;

            case "quality":
                _logger.LogInformation("质量异常操作：itemId={ItemId}, action={Action}, userId={UserId}", itemId, actionKey, userId);
                // 后续任务中接入真实异常处理逻辑
                break;

            case "wf":
                // WfWorkItem 操作：通过 IWorkItemService 执行状态流转
                var wfIdStr = itemId.Substring(3); // skip "wf-"
                if (!long.TryParse(wfIdStr, out var wfItemId))
                    throw new ArgumentException($"无效的 WF 工作项ID: {wfIdStr}");
                using (var scope = _scopeFactory.CreateScope())
                {
                    var wfService = scope.ServiceProvider.GetRequiredService<IWorkItemService>();
                    if (actionKey == "start")
                        await wfService.StartAsync(wfItemId, userId);
                    else if (actionKey == "complete")
                        await wfService.CompleteAsync(wfItemId, userId);
                    else if (actionKey == "cancel")
                        await wfService.CancelAsync(wfItemId, userId);
                    else
                        _logger.LogInformation("WF 工作项操作：itemId={ItemId}, action={Action}", itemId, actionKey);
                }
                break;

            default:
                throw new NotSupportedException($"来源 '{source}' 暂不支持内联操作");
        }

        // 操作完成后通过 IWorkHubNotifier 推送 SignalR 通知
        if (actionKey == "withdraw")
        {
            // 撤回操作：移除工作项
            await _workHubNotifier.RemoveWorkItemAsync(userId, 0, source);
        }
        else if (long.TryParse(itemId.Split('-').Last(), out var parsedItemId))
        {
            await _workHubNotifier.RemoveWorkItemAsync(userId, parsedItemId, source);
        }
        await _workHubNotifier.RefreshStatsAsync(userId);

        return true;
    }

    // ===== 辅助方法 =====

    /// <summary>对合并后的工作项批量回填业务类型字段。</summary>
    private static void EnrichBizTypes(IEnumerable<DtoWorkItemDto> items)
    {
        foreach (var item in items)
        {
            var (key, label) = ResolveBizType(item);
            item.BizTypeKey = key;
            item.BizTypeLabel = label;
        }
    }

    private static string MapCardFlowPriority(int priority) => priority switch
    {
        >= 5 => "urgent",
        4 => "high",
        <= 1 => "low",
        _ => "normal",
    };

    private static string MapQualityPriority(int priority) => priority switch
    {
        3 => "urgent",
        2 => "high",
        1 => "normal",
        _ => "low",
    };

    private static string MapTaskPriority(int priority) => priority switch
    {
        3 => "urgent",
        2 => "high",
        1 => "normal",
        _ => "low",
    };

    private static string MapTaskStatusText(int status) => status switch
    {
        0 => "待开始",
        1 => "进行中",
        2 => "已暂停",
        3 => "已完成",
        4 => "已取消",
        _ => "未知",
    };

    // ===== 工作项数据增强：relatedLinks 填充 + action 字段扩展 =====

    /// <summary>
    /// 根据来源类型填充 relatedLinks，并对 actions 应用语义化扩展字段
    /// </summary>
    private static void EnrichWorkItem(DtoWorkItemDto item)
    {
        // 1. relatedLinks 填充
        var relatedLinks = BuildRelatedLinks(item);
        if (relatedLinks.Count > 0)
        {
            // 避免重复：仅在尚未填充时追加
            if (item.RelatedLinks == null || item.RelatedLinks.Count == 0)
            {
                item.RelatedLinks = relatedLinks;
            }
            else
            {
                foreach (var link in relatedLinks)
                {
                    if (!item.RelatedLinks.Any(l => l.Route == link.Route))
                        item.RelatedLinks.Add(link);
                }
            }
        }

        // 2. action 字段扩展
        if (item.Actions != null)
        {
            foreach (var action in item.Actions)
            {
                EnrichAction(action);
            }
        }
    }

    /// <summary>
    /// 根据来源类型构建 relatedLinks
    /// </summary>
    private static List<RelatedLinkDto> BuildRelatedLinks(DtoWorkItemDto item)
    {
        var links = new List<RelatedLinkDto>();
        var sourceId = ResolveSourceId(item);
        if (string.IsNullOrEmpty(sourceId)) return links;

        switch (item.Source)
        {
            case "cardflow":
                links.Add(new RelatedLinkDto
                {
                    Label = "卡片运行视图",
                    Route = item.DetailRoute ?? $"/cardflow/cards/{sourceId}",
                    Icon = "audit",
                    Summary = "打开 CardFlow 同一张卡片详情，查看审批、条件流转和动态审批记录",
                });
                break;
            case "datacenter":
                links.Add(new RelatedLinkDto
                {
                    Label = "批次详情",
                    Route = $"/datacenter/batch/{sourceId}",
                    Icon = "database",
                    Summary = "查看批次处理详情",
                });
                break;
            case "quality":
                links.Add(new RelatedLinkDto
                {
                    Label = "质量异常详情",
                    Route = $"/quality/issue/{sourceId}",
                    Icon = "warning",
                    Summary = "查看质量异常处理记录",
                });
                break;
            // 其他来源暂不处理
        }
        return links;
    }

    /// <summary>
    /// 解析工作项的业务来源ID。优先取 Metadata 中的业务键，回退到 Id 后缀。
    /// </summary>
    private static string ResolveSourceId(DtoWorkItemDto item)
    {
        // 来源专用键
        switch (item.Source)
        {
            case "cardflow":
                if (item.Metadata.TryGetValue("cardId", out var cardId) && cardId != null)
                {
                    var s = cardId.ToString();
                    if (!string.IsNullOrEmpty(s) && s != "0") return s!;
                }
                if (item.Metadata.TryGetValue("cardflowTodoId", out var todoId) && todoId != null)
                {
                    var s = todoId.ToString();
                    if (!string.IsNullOrEmpty(s) && s != "0") return s!;
                }
                break;
            case "datacenter":
                if (item.Metadata.TryGetValue("bizId", out var bid) && bid != null)
                {
                    var s = bid.ToString();
                    if (!string.IsNullOrEmpty(s) && s != "0") return s!;
                }
                if (item.Metadata.TryGetValue("batchId", out var batchId) && batchId != null)
                {
                    var s = batchId.ToString();
                    if (!string.IsNullOrEmpty(s) && s != "0") return s!;
                }
                break;
            case "quality":
                if (item.Metadata.TryGetValue("exceptionNo", out var eno) && eno != null)
                {
                    var s = eno.ToString();
                    if (!string.IsNullOrEmpty(s)) return s!;
                }
                break;
        }

        // 回退：从 Id 提取最后一段数字（格式如 "oa-123" / "wf-456" / "quality-789"）
        if (!string.IsNullOrEmpty(item.Id))
        {
            var idx = item.Id.LastIndexOf('-');
            if (idx >= 0 && idx < item.Id.Length - 1)
            {
                return item.Id.Substring(idx + 1);
            }
        }
        return string.Empty;
    }

    /// <summary>
    /// 根据 action 的 label/key 语义补全 type / finalizes / needsConfirm / confirmSummary 字段
    /// </summary>
    private static void EnrichAction(WorkItemActionDto action)
    {
        var label = action.Label ?? string.Empty;
        var key = (action.Key ?? string.Empty).ToLowerInvariant();

        // 通过/批准类
        if (label.Contains("通过") || label.Contains("批准") || label.Contains("同意") ||
            key == "approve" || key == "agree" || key == "pass")
        {
            action.Type = "primary";
            action.Finalizes = true;
            action.NeedsConfirm = false;
            return;
        }

        // 驳回/拒绝类
        if (label.Contains("驳回") || label.Contains("拒绝") || label.Contains("不同意") ||
            key == "reject" || key == "refuse" || key == "deny")
        {
            action.Type = "danger";
            action.Finalizes = true;
            action.NeedsConfirm = true;
            action.ConfirmSummary = new List<string>
            {
                "驳回后将退回至发起人",
                "此操作不可撤销",
            };
            return;
        }

        // 转交/加签类
        if (label.Contains("转交") || label.Contains("加签") || label.Contains("转办") ||
            key == "transfer" || key == "delegate" || key == "countersign")
        {
            action.Type = "default";
            action.Finalizes = false;
            action.NeedsConfirm = false;
            return;
        }

        // 催办/提醒类
        if (label.Contains("催办") || label.Contains("提醒") ||
            key == "urge" || key == "remind")
        {
            action.Type = "secondary";
            action.Finalizes = false;
            action.NeedsConfirm = false;
            return;
        }

        // 撤回类（已有 danger 样式，补充 finalizes/needsConfirm 语义）
        if (label.Contains("撤回") || key == "withdraw")
        {
            action.Type ??= "danger";
            action.Finalizes ??= true;
            action.NeedsConfirm ??= true;
            action.ConfirmSummary ??= new List<string>
            {
                "撤回后流程将终止",
                "此操作不可撤销",
            };
        }
    }
}
