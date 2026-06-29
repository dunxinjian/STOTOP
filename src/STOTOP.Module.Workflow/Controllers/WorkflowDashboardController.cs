using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Workflow.Entities;
using STOTOP.Module.Workflow.Enums;

namespace STOTOP.Module.Workflow.Controllers;

[Authorize]
[ApiController]
[Route("api/workflow/dashboard")]
public class WorkflowDashboardController : ControllerBase
{
    private readonly STOTOPDbContext _db;

    public WorkflowDashboardController(STOTOPDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// 总览统计
    /// </summary>
    [HttpGet("overview")]
    public async Task<ApiResult<DashboardOverviewDto>> GetOverview()
    {
        var now = DateTime.Now;
        var todayStart = now.Date;

        var query = _db.Set<WfWorkItem>().AsNoTracking();

        var totalPending = await query.CountAsync(x => x.FStatus == (int)WorkItemStatus.Pending);
        var totalInProgress = await query.CountAsync(x => x.FStatus == (int)WorkItemStatus.InProgress);
        var completedToday = await query.CountAsync(x =>
            x.FStatus == (int)WorkItemStatus.Completed && x.FCompletedTime >= todayStart);
        var overdueCount = await query.CountAsync(x =>
            x.FDeadline != null && x.FDeadline < now &&
            x.FStatus != (int)WorkItemStatus.Completed && x.FStatus != (int)WorkItemStatus.Cancelled);

        // 平均处理时长（小时）
        var completedItems = await query
            .Where(x => x.FStatus == (int)WorkItemStatus.Completed && x.FCompletedTime != null)
            .Select(x => new { x.FCreateTime, x.FCompletedTime })
            .ToListAsync();

        var avgProcessHours = completedItems.Count > 0
            ? completedItems.Average(x => (x.FCompletedTime!.Value - x.FCreateTime).TotalHours)
            : 0;

        // SLA达标率
        var completedWithDeadline = await query
            .Where(x => x.FStatus == (int)WorkItemStatus.Completed && x.FDeadline != null && x.FCompletedTime != null)
            .Select(x => new { x.FDeadline, x.FCompletedTime })
            .ToListAsync();

        var slaRate = completedWithDeadline.Count > 0
            ? (double)completedWithDeadline.Count(x => x.FCompletedTime!.Value <= x.FDeadline!.Value)
              / completedWithDeadline.Count * 100
            : 100;

        var dto = new DashboardOverviewDto
        {
            TotalPending = totalPending,
            TotalInProgress = totalInProgress,
            CompletedToday = completedToday,
            OverdueCount = overdueCount,
            AvgProcessHours = Math.Round(avgProcessHours, 1),
            SlaRate = Math.Round(slaRate, 1)
        };

        return ApiResult<DashboardOverviewDto>.Success(dto);
    }

    /// <summary>
    /// 按状态分组统计
    /// </summary>
    [HttpGet("by-status")]
    public async Task<ApiResult<List<StatusGroupDto>>> GetByStatus()
    {
        var groups = await _db.Set<WfWorkItem>().AsNoTracking()
            .GroupBy(x => x.FStatus)
            .Select(g => new StatusGroupDto
            {
                Status = g.Key,
                Count = g.Count()
            })
            .ToListAsync();

        // 填充 StatusText
        foreach (var g in groups)
        {
            g.StatusText = g.Status switch
            {
                0 => "待处理",
                1 => "处理中",
                2 => "已完成",
                3 => "已取消",
                4 => "已超时",
                _ => "未知"
            };
        }

        return ApiResult<List<StatusGroupDto>>.Success(groups);
    }

    /// <summary>
    /// 按模块分组
    /// </summary>
    [HttpGet("by-module")]
    public async Task<ApiResult<List<ModuleGroupDto>>> GetByModule()
    {
        var groups = await _db.Set<WfWorkItem>().AsNoTracking()
            .GroupBy(x => x.FModule ?? "未分类")
            .Select(g => new ModuleGroupDto
            {
                Module = g.Key,
                PendingCount = g.Count(x => x.FStatus == (int)WorkItemStatus.Pending),
                InProgressCount = g.Count(x => x.FStatus == (int)WorkItemStatus.InProgress),
                CompletedCount = g.Count(x => x.FStatus == (int)WorkItemStatus.Completed)
            })
            .ToListAsync();

        return ApiResult<List<ModuleGroupDto>>.Success(groups);
    }

    /// <summary>
    /// 按处理人分组（人效排行）
    /// </summary>
    [HttpGet("by-assignee")]
    public async Task<ApiResult<List<AssigneeStatsDto>>> GetByAssignee()
    {
        var groups = await _db.Set<WfWorkItem>().AsNoTracking()
            .Where(x => x.FAssigneeId != null)
            .GroupBy(x => new { x.FAssigneeId, x.FAssigneeName })
            .Select(g => new AssigneeStatsDto
            {
                AssigneeId = g.Key.FAssigneeId!.Value,
                AssigneeName = g.Key.FAssigneeName ?? "未知",
                PendingCount = g.Count(x => x.FStatus == (int)WorkItemStatus.Pending),
                InProgressCount = g.Count(x => x.FStatus == (int)WorkItemStatus.InProgress),
                CompletedCount = g.Count(x => x.FStatus == (int)WorkItemStatus.Completed),
            })
            .ToListAsync();

        // 计算平均处理时长（需在内存中计算）
        foreach (var g in groups)
        {
            var completedItems = await _db.Set<WfWorkItem>().AsNoTracking()
                .Where(x => x.FAssigneeId == g.AssigneeId
                    && x.FStatus == (int)WorkItemStatus.Completed
                    && x.FCompletedTime != null)
                .Select(x => new { x.FCreateTime, x.FCompletedTime })
                .ToListAsync();

            g.AvgProcessHours = completedItems.Count > 0
                ? Math.Round(completedItems.Average(x => (x.FCompletedTime!.Value - x.FCreateTime).TotalHours), 1)
                : 0;
        }

        return ApiResult<List<AssigneeStatsDto>>.Success(groups.OrderByDescending(x => x.CompletedCount).ToList());
    }

    /// <summary>
    /// 趋势数据（每日创建/完成数）
    /// </summary>
    [HttpGet("trend")]
    public async Task<ApiResult<TrendDataDto>> GetTrend([FromQuery] int days = 7)
    {
        if (days < 1) days = 7;
        if (days > 90) days = 90;

        var now = DateTime.Now.Date;
        var startDate = now.AddDays(-(days - 1));

        var items = await _db.Set<WfWorkItem>().AsNoTracking()
            .Where(x => x.FCreateTime >= startDate || (x.FCompletedTime != null && x.FCompletedTime >= startDate))
            .Select(x => new { x.FCreateTime, x.FCompletedTime, x.FStatus })
            .ToListAsync();

        var dates = new List<string>();
        var createdCounts = new List<int>();
        var completedCounts = new List<int>();

        for (var d = startDate; d <= now; d = d.AddDays(1))
        {
            dates.Add(d.ToString("MM-dd"));
            createdCounts.Add(items.Count(x => x.FCreateTime.Date == d));
            completedCounts.Add(items.Count(x =>
                x.FCompletedTime != null && x.FCompletedTime.Value.Date == d));
        }

        var dto = new TrendDataDto
        {
            Dates = dates,
            CreatedCounts = createdCounts,
            CompletedCounts = completedCounts
        };

        return ApiResult<TrendDataDto>.Success(dto);
    }

    /// <summary>
    /// 超时工作项列表
    /// </summary>
    [HttpGet("overdue-items")]
    public async Task<ApiResult<OverduePageDto>> GetOverdueItems([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var now = DateTime.Now;

        var query = _db.Set<WfWorkItem>().AsNoTracking()
            .Where(x => x.FDeadline != null && x.FDeadline < now
                && x.FStatus != (int)WorkItemStatus.Completed
                && x.FStatus != (int)WorkItemStatus.Cancelled)
            .OrderBy(x => x.FDeadline);

        var total = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new OverdueItemDto
            {
                Id = x.FID,
                Uid = x.FUID,
                Title = x.FTitle,
                Module = x.FModule,
                AssigneeId = x.FAssigneeId,
                AssigneeName = x.FAssigneeName,
                Status = x.FStatus,
                Priority = x.FPriority,
                Deadline = x.FDeadline!.Value,
                CreateTime = x.FCreateTime,
                OverdueHours = Math.Round((now - x.FDeadline!.Value).TotalHours, 1)
            })
            .ToListAsync();

        var dto = new OverduePageDto
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            Items = items
        };

        return ApiResult<OverduePageDto>.Success(dto);
    }
}

#region DTOs

public class DashboardOverviewDto
{
    public int TotalPending { get; set; }
    public int TotalInProgress { get; set; }
    public int CompletedToday { get; set; }
    public int OverdueCount { get; set; }
    public double AvgProcessHours { get; set; }
    public double SlaRate { get; set; }
}

public class StatusGroupDto
{
    public int Status { get; set; }
    public string StatusText { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class ModuleGroupDto
{
    public string Module { get; set; } = string.Empty;
    public int PendingCount { get; set; }
    public int InProgressCount { get; set; }
    public int CompletedCount { get; set; }
}

public class AssigneeStatsDto
{
    public long AssigneeId { get; set; }
    public string AssigneeName { get; set; } = string.Empty;
    public int PendingCount { get; set; }
    public int InProgressCount { get; set; }
    public int CompletedCount { get; set; }
    public double AvgProcessHours { get; set; }
}

public class TrendDataDto
{
    public List<string> Dates { get; set; } = new();
    public List<int> CreatedCounts { get; set; } = new();
    public List<int> CompletedCounts { get; set; } = new();
}

public class OverdueItemDto
{
    public long Id { get; set; }
    public string Uid { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Module { get; set; }
    public long? AssigneeId { get; set; }
    public string? AssigneeName { get; set; }
    public int Status { get; set; }
    public int Priority { get; set; }
    public DateTime Deadline { get; set; }
    public DateTime CreateTime { get; set; }
    public double OverdueHours { get; set; }
}

public class OverduePageDto
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<OverdueItemDto> Items { get; set; } = new();
}

#endregion
