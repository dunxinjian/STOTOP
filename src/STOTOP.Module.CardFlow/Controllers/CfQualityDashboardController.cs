using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.Workflow.Entities;

namespace STOTOP.Module.CardFlow.Controllers;

/// <summary>质量中心 Dashboard - 汇总统计、趋势、工作量、超时预警</summary>
[Authorize]
[ApiController]
[Route("api/quality-center/dashboard")]
public class CfQualityDashboardController : ControllerBase
{
    private readonly STOTOPDbContext _context;

    public CfQualityDashboardController(STOTOPDbContext context)
    {
        _context = context;
    }

    private const string QualityIssueCategory = "QualityIssue";

    /// <summary>汇总统计：各状态数量 + 按问题类型分布</summary>
    [HttpGet("summary")]
    public async Task<ApiResult<QualityDashboardSummaryDto>> GetSummary([FromQuery] long? orgId)
    {
        var query = _context.Set<WfWorkItem>()
            .Where(w => w.FCategory == QualityIssueCategory);

        if (orgId.HasValue)
            query = query.Where(w => w.FOrgId == orgId.Value);

        // 状态统计
        var statusGroups = await query
            .GroupBy(w => w.FStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        var pending = statusGroups.Where(s => s.Status == 0).Sum(s => s.Count);
        var inProgress = statusGroups.Where(s => s.Status == 1).Sum(s => s.Count);
        var resolved = statusGroups.Where(s => s.Status == 2).Sum(s => s.Count);

        // 超时计数：FIsOverdue=true 且 状态不是已完成/已取消
        var overdueCount = await query
            .Where(w => w.FIsOverdue && w.FStatus != 2 && w.FStatus != 3)
            .CountAsync();

        // 按类型分布（通过 BizType 关联到 IssueType Code）
        var bizTypeGroups = await query
            .Where(w => w.FBizType != null)
            .GroupBy(w => w.FBizType!)
            .Select(g => new { Code = g.Key, Count = g.Count() })
            .ToListAsync();

        // 获取 IssueType 名称映射
        var codes = bizTypeGroups.Select(b => b.Code).ToList();
        var issueTypes = await _context.Set<CfQualityIssueType>()
            .Where(t => codes.Contains(t.FCode))
            .Select(t => new { t.FCode, t.FName })
            .ToListAsync();

        var typeNameMap = issueTypes.ToDictionary(t => t.FCode, t => t.FName);

        var byType = bizTypeGroups.Select(b => new QualityIssueTypeCountDto
        {
            Code = b.Code,
            Name = typeNameMap.GetValueOrDefault(b.Code, b.Code),
            Count = b.Count
        }).OrderByDescending(t => t.Count).ToList();

        return ApiResult<QualityDashboardSummaryDto>.Success(new QualityDashboardSummaryDto
        {
            Pending = pending,
            InProgress = inProgress,
            Resolved = resolved,
            Overdue = overdueCount,
            ByType = byType
        });
    }

    /// <summary>时效趋势：近N天每日产生量/完成量</summary>
    [HttpGet("trend")]
    public async Task<ApiResult<QualityTrendDto>> GetTrend([FromQuery] long? orgId, [FromQuery] int days = 7)
    {
        if (days < 1) days = 7;
        if (days > 90) days = 90;

        var startDate = DateTime.Today.AddDays(-days + 1);

        var query = _context.Set<WfWorkItem>()
            .Where(w => w.FCategory == QualityIssueCategory);

        if (orgId.HasValue)
            query = query.Where(w => w.FOrgId == orgId.Value);

        // 每日创建量
        var createdByDay = await query
            .Where(w => w.FCreateTime >= startDate)
            .GroupBy(w => w.FCreateTime.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToListAsync();

        // 每日完成量
        var completedByDay = await query
            .Where(w => w.FCompletedTime != null && w.FCompletedTime.Value >= startDate)
            .GroupBy(w => w.FCompletedTime!.Value.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToListAsync();

        var createdMap = createdByDay.ToDictionary(d => d.Date, d => d.Count);
        var completedMap = completedByDay.ToDictionary(d => d.Date, d => d.Count);

        var trendDays = new List<QualityTrendDayDto>();
        for (var date = startDate; date <= DateTime.Today; date = date.AddDays(1))
        {
            trendDays.Add(new QualityTrendDayDto
            {
                Date = date.ToString("yyyy-MM-dd"),
                Created = createdMap.GetValueOrDefault(date, 0),
                Completed = completedMap.GetValueOrDefault(date, 0)
            });
        }

        return ApiResult<QualityTrendDto>.Success(new QualityTrendDto { Days = trendDays });
    }

    /// <summary>处理人工作量排行</summary>
    [HttpGet("workload")]
    public async Task<ApiResult<QualityWorkloadDto>> GetWorkload([FromQuery] long? orgId)
    {
        var query = _context.Set<WfWorkItem>()
            .Where(w => w.FCategory == QualityIssueCategory && w.FAssigneeId != null);

        if (orgId.HasValue)
            query = query.Where(w => w.FOrgId == orgId.Value);

        var workloadRaw = await query
            .GroupBy(w => new { w.FAssigneeId, w.FAssigneeName })
            .Select(g => new
            {
                AssigneeId = g.Key.FAssigneeId,
                AssigneeName = g.Key.FAssigneeName ?? "未知",
                Pending = g.Count(w => w.FStatus == 0),
                InProgress = g.Count(w => w.FStatus == 1),
                Completed = g.Count(w => w.FStatus == 2),
                Total = g.Count()
            })
            .OrderByDescending(x => x.Total)
            .Take(20)
            .ToListAsync();

        var items = workloadRaw.Select(w => new QualityWorkloadItemDto
        {
            AssigneeId = w.AssigneeId,
            AssigneeName = w.AssigneeName,
            Pending = w.Pending,
            InProgress = w.InProgress,
            Completed = w.Completed,
            Total = w.Total
        }).ToList();

        return ApiResult<QualityWorkloadDto>.Success(new QualityWorkloadDto { Items = items });
    }

    /// <summary>超时预警列表</summary>
    [HttpGet("overdue")]
    public async Task<ApiResult<List<QualityOverdueItemDto>>> GetOverdue([FromQuery] long? orgId)
    {
        var query = _context.Set<WfWorkItem>()
            .Where(w => w.FCategory == QualityIssueCategory
                        && w.FIsOverdue
                        && w.FStatus != 2
                        && w.FStatus != 3);

        if (orgId.HasValue)
            query = query.Where(w => w.FOrgId == orgId.Value);

        var items = await query
            .OrderByDescending(w => w.FCreateTime)
            .Take(50)
            .Select(w => new QualityOverdueItemDto
            {
                Id = w.FID,
                Title = w.FTitle,
                BizType = w.FBizType,
                OrgId = w.FOrgId,
                AssigneeName = w.FAssigneeName,
                Status = w.FStatus,
                CreateTime = w.FCreateTime,
                Deadline = w.FDeadline
            })
            .ToListAsync();

        return ApiResult<List<QualityOverdueItemDto>>.Success(items);
    }
}
