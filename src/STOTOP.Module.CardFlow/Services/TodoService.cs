using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.CardFlow.Services.Interfaces;

namespace STOTOP.Module.CardFlow.Services;

public class TodoService : ITodoService
{
    private readonly STOTOPDbContext _dbContext;
    private readonly ILogger<TodoService> _logger;

    public TodoService(STOTOPDbContext dbContext, ILogger<TodoService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<PagedResult<TodoItemDto>> GetMyTodosAsync(long userId, TodoQueryRequest request)
    {
        var query = _dbContext.Set<CfTodoItem>()
            .Where(t => t.FHandlerId == userId && t.FType == "todo");

        if (!string.IsNullOrEmpty(request.Status))
            query = query.Where(t => t.FStatus == request.Status);
        if (request.FlowId.HasValue)
        {
            var cardIds = await _dbContext.Set<CfCard>()
                .Where(c => c.FFlowDefinitionId == request.FlowId.Value)
                .Select(c => c.FID)
                .ToListAsync();
            query = query.Where(t => cardIds.Contains(t.FCardId));
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(t => t.FCreatedTime)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Join(_dbContext.Set<CfCard>(), t => t.FCardId, c => c.FID, (t, c) => new { Todo = t, Card = c })
            .Join(_dbContext.Set<CfFlowDefinition>(), x => x.Card.FFlowDefinitionId, f => f.FID, (x, f) => new TodoItemDto
            {
                Id = x.Todo.FID,
                CardId = x.Todo.FCardId,
                CardNumber = x.Card.FCardNumber,
                Title = x.Todo.FTitle,
                FlowName = f.FFlowName,
                Type = x.Todo.FType,
                Status = x.Todo.FStatus,
                Priority = x.Todo.FPriority,
                InitiatorName = x.Card.FInitiatorName,
                CreatedTime = x.Todo.FCreatedTime
            })
            .ToListAsync();

        return new PagedResult<TodoItemDto>
        {
            Items = items,
            Total = totalCount,
            PageIndex = request.Page,
            PageSize = request.PageSize
        };
    }

    public async Task<PagedResult<TodoItemDto>> GetMyCcAsync(long userId, TodoQueryRequest request)
    {
        var query = _dbContext.Set<CfTodoItem>()
            .Where(t => t.FHandlerId == userId && t.FType == "cc");

        if (!string.IsNullOrEmpty(request.Status))
            query = query.Where(t => t.FStatus == request.Status);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(t => t.FCreatedTime)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Join(_dbContext.Set<CfCard>(), t => t.FCardId, c => c.FID, (t, c) => new { Todo = t, Card = c })
            .Join(_dbContext.Set<CfFlowDefinition>(), x => x.Card.FFlowDefinitionId, f => f.FID, (x, f) => new TodoItemDto
            {
                Id = x.Todo.FID,
                CardId = x.Todo.FCardId,
                CardNumber = x.Card.FCardNumber,
                Title = x.Todo.FTitle,
                FlowName = f.FFlowName,
                Type = x.Todo.FType,
                Status = x.Todo.FStatus,
                Priority = x.Todo.FPriority,
                InitiatorName = x.Card.FInitiatorName,
                CreatedTime = x.Todo.FCreatedTime
            })
            .ToListAsync();

        return new PagedResult<TodoItemDto>
        {
            Items = items,
            Total = totalCount,
            PageIndex = request.Page,
            PageSize = request.PageSize
        };
    }

    public async Task<TodoCountDto> GetCountAsync(long userId)
    {
        var todoCount = await _dbContext.Set<CfTodoItem>()
            .CountAsync(t => t.FHandlerId == userId && t.FType == "todo" && t.FStatus == "pending");

        var initiatedCount = await _dbContext.Set<CfCard>()
            .CountAsync(c => c.FInitiatorId == userId && c.FStatus != "draft" && c.FStatus != "completed" && c.FStatus != "terminated");

        var ccCount = await _dbContext.Set<CfTodoItem>()
            .CountAsync(t => t.FHandlerId == userId && t.FType == "cc" && t.FStatus == "pending");

        return new TodoCountDto
        {
            Todo = todoCount,
            Initiated = initiatedCount,
            Cc = ccCount
        };
    }

    public async Task<TodoStatsDto> GetStatsAsync(long orgId)
    {
        return await GetStatsAsync(new TodoStatsRequest { OrgId = orgId });
    }

    public async Task<TodoStatsDto> GetStatsAsync(TodoStatsRequest request)
    {
        var orgId = request.OrgId;
        var timeoutHours = request.TimeoutHours <= 0 ? 24 : request.TimeoutHours;
        var now = DateTime.Now;
        var todayStart = now.Date;
        var tomorrowStart = todayStart.AddDays(1);
        var rangeStart = request.StartDate;
        var rangeEnd = request.EndDate?.Date.AddDays(1); // 含当天

        // 基础待办集（按组织隔离，仅 todo 类型）
        var todoBase = _dbContext.Set<CfTodoItem>()
            .Where(t => t.FOrgId == orgId && t.FType == "todo");

        // 可选：按流程过滤
        if (request.FlowId.HasValue)
        {
            var flowId = request.FlowId.Value;
            var cardIds = await _dbContext.Set<CfCard>()
                .Where(c => c.FFlowDefinitionId == flowId)
                .Select(c => c.FID)
                .ToListAsync();
            todoBase = todoBase.Where(t => cardIds.Contains(t.FCardId));
        }

        // 日期范围（限定创建时间）
        var todoRange = todoBase;
        if (rangeStart.HasValue)
            todoRange = todoRange.Where(t => t.FCreatedTime >= rangeStart.Value);
        if (rangeEnd.HasValue)
            todoRange = todoRange.Where(t => t.FCreatedTime < rangeEnd.Value);

        // 总待办数（当前待处理，不受日期范围限制）
        var totalPending = await todoBase.CountAsync(t => t.FStatus == "pending");

        // 今日完成数
        var todayCompleted = await todoBase.CountAsync(t =>
            t.FStatus == "completed" && t.FCompletedTime != null &&
            t.FCompletedTime >= todayStart && t.FCompletedTime < tomorrowStart);

        // 范围内已完成待办用于平均时长计算
        var completedInRange = await todoRange
            .Where(t => t.FStatus == "completed" && t.FCompletedTime != null)
            .Select(t => new { t.FCreatedTime, Done = t.FCompletedTime!.Value, t.FCardId })
            .ToListAsync();

        double avgHours = completedInRange.Count == 0
            ? 0
            : completedInRange.Average(x => (x.Done - x.FCreatedTime).TotalHours);

        // 超时率：在范围内完成中超过阈值的比例 + 当前未完成超时的使用 totalPending 计算
        var timeoutCompleted = completedInRange.Count(x => (x.Done - x.FCreatedTime).TotalHours > timeoutHours);
        var pendingTimeout = await todoBase.CountAsync(t =>
            t.FStatus == "pending" && EF.Functions.DateDiffHour(t.FCreatedTime, now) > timeoutHours);
        var denom = completedInRange.Count + totalPending;
        double timeoutRate = denom == 0 ? 0 : Math.Round((double)(timeoutCompleted + pendingTimeout) / denom * 100, 2);

        // 按流程类型统计
        var flowGroupRaw = await todoRange
            .Join(_dbContext.Set<CfCard>(), t => t.FCardId, c => c.FID, (t, c) => new { Todo = t, c.FFlowDefinitionId })
            .Join(_dbContext.Set<CfFlowDefinition>(), x => x.FFlowDefinitionId, f => f.FID, (x, f) => new
            {
                FlowName = f.FFlowName,
                x.Todo.FStatus,
                x.Todo.FCreatedTime,
                x.Todo.FCompletedTime
            })
            .ToListAsync();

        var flowStats = flowGroupRaw
            .GroupBy(x => x.FlowName)
            .Select(g =>
            {
                var done = g.Where(x => x.FStatus == "completed" && x.FCompletedTime.HasValue).ToList();
                var pending = g.Count(x => x.FStatus == "pending");
                var avg = done.Count == 0 ? 0 : done.Average(x => (x.FCompletedTime!.Value - x.FCreatedTime).TotalHours);
                var tCnt = done.Count(x => (x.FCompletedTime!.Value - x.FCreatedTime).TotalHours > timeoutHours);
                var dnm = done.Count + pending;
                var tRate = dnm == 0 ? 0 : Math.Round((double)tCnt / dnm * 100, 2);
                return new FlowTodoStat
                {
                    FlowName = g.Key,
                    PendingCount = pending,
                    CompletedCount = done.Count,
                    AvgProcessHours = Math.Round(avg, 2),
                    TimeoutRate = tRate
                };
            })
            .OrderByDescending(s => s.PendingCount + s.CompletedCount)
            .ToList();

        // 趋势：按完成日分组计算平均处理时长
        var trend = completedInRange
            .GroupBy(x => x.Done.Date)
            .OrderBy(g => g.Key)
            .Select(g => new TodoTrendPoint
            {
                Date = g.Key.ToString("yyyy-MM-dd"),
                AvgProcessHours = Math.Round(g.Average(x => (x.Done - x.FCreatedTime).TotalHours), 2)
            })
            .ToList();

        return new TodoStatsDto
        {
            TotalPending = totalPending,
            AvgProcessHours = Math.Round(avgHours, 2),
            TimeoutRate = timeoutRate,
            TodayCompleted = todayCompleted,
            FlowStats = flowStats,
            Trend = trend
        };
    }

    public async Task<long> CreateTodoAsync(long cardId, long stageInstanceId, long handlerId, string handlerName, string title, string type = "todo", string? pushChannel = null)
    {
        var card = await _dbContext.Set<CfCard>().FirstOrDefaultAsync(c => c.FID == cardId)
            ?? throw new InvalidOperationException("卡片不存在");

        var todoItem = new CfTodoItem
        {
            FCardId = cardId,
            FStageInstanceId = stageInstanceId,
            FHandlerId = handlerId,
            FHandlerName = handlerName,
            FTitle = title,
            FType = type,
            FStatus = "pending",
            FPriority = 3,
            FPushStatus = "pending",
            FPushChannel = pushChannel,
            FCreatedTime = DateTime.Now,
            FOrgId = card.FOrgId
        };

        _dbContext.Set<CfTodoItem>().Add(todoItem);
        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Created {Type} item for card {CardId}, handler {HandlerId}", type, cardId, handlerId);
        return todoItem.FID;
    }

    public async Task CompleteTodoAsync(long todoItemId)
    {
        var item = await _dbContext.Set<CfTodoItem>().FirstOrDefaultAsync(t => t.FID == todoItemId)
            ?? throw new InvalidOperationException("待办记录不存在");

        item.FStatus = "completed";
        item.FCompletedTime = DateTime.Now;
        await _dbContext.SaveChangesAsync();
    }

    public async Task CancelTodoAsync(long todoItemId)
    {
        var item = await _dbContext.Set<CfTodoItem>().FirstOrDefaultAsync(t => t.FID == todoItemId)
            ?? throw new InvalidOperationException("待办记录不存在");

        item.FStatus = "cancelled";
        item.FCompletedTime = DateTime.Now;
        await _dbContext.SaveChangesAsync();
    }
}
