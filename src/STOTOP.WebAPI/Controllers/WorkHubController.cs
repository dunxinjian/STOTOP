using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Workflow.Entities;
using STOTOP.WebAPI.Dtos.WorkHub;
using STOTOP.WebAPI.Services;
using DtoWorkItemDto = STOTOP.WebAPI.Dtos.WorkHub.WorkItemDto;

namespace STOTOP.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WorkHubController : ControllerBase
{
    private readonly IWorkHubService _workHubService;
    private readonly STOTOPDbContext _dbContext;

    public WorkHubController(IWorkHubService workHubService, STOTOPDbContext dbContext)
    {
        _workHubService = workHubService;
        _dbContext = dbContext;
    }

    private long GetUserId() => long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
    private long GetOrgId() => (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);

    /// <summary>
    /// 获取工作项列表（分页 + 筛选）
    /// </summary>
    [HttpGet("items")]
    public async Task<ApiResult<PagedResult<DtoWorkItemDto>>> GetItems(
        [FromQuery] string? category,
        [FromQuery] string? priority,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var userId = GetUserId();
        var orgId = GetOrgId();

        var result = await _workHubService.GetWorkItemsAsync(userId, orgId, category, priority, page, pageSize);
        return ApiResult<PagedResult<DtoWorkItemDto>>.Success(result);
    }

    /// <summary>
    /// 获取工作台统计信息
    /// </summary>
    [HttpGet("stats")]
    public async Task<ApiResult<WorkHubStatsDto>> GetStats()
    {
        var userId = GetUserId();
        var orgId = GetOrgId();

        var stats = await _workHubService.GetStatsAsync(userId, orgId);
        return ApiResult<WorkHubStatsDto>.Success(stats);
    }

    /// <summary>
    /// 获取工作项列表 + 统计信息（合并接口，用于初始化加载）
    /// </summary>
    [HttpGet("items-with-stats")]
    public async Task<ApiResult<WorkItemsWithStatsDto>> GetItemsWithStats(
        [FromQuery] string? priority,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 15)
    {
        var userId = GetUserId();
        var orgId = GetOrgId();

        var result = await _workHubService.GetWorkItemsWithStatsAsync(userId, orgId, priority, page, pageSize);
        return ApiResult<WorkItemsWithStatsDto>.Success(result);
    }

    /// <summary>
    /// 执行工作项内联操作
    /// </summary>
    [HttpPost("items/{id}/actions/{actionKey}")]
    public async Task<ApiResult<bool>> ExecuteAction(string id, string actionKey)
    {
        var userId = GetUserId();
        var result = await _workHubService.ExecuteActionAsync(userId, id, actionKey);
        return ApiResult<bool>.Success(result);
    }

    /// <summary>
    /// 质量问题汇总：待处理总数、今日新增、超时预警数
    /// </summary>
    [HttpGet("quality-summary")]
    public async Task<ApiResult<QualitySummaryDto>> GetQualitySummary()
    {
        var orgId = GetOrgId();
        var today = DateTime.Today;

        var query = _dbContext.Set<WfWorkItem>()
            .Where(w => w.FCategory == "QualityIssue");

        if (orgId > 0)
            query = query.Where(w => w.FOrgId == orgId);

        var pendingTotal = await query
            .Where(w => w.FStatus == 0 || w.FStatus == 1)
            .CountAsync();

        var todayNew = await query
            .Where(w => w.FCreateTime >= today)
            .CountAsync();

        var overdueWarning = await query
            .Where(w => w.FIsOverdue && w.FStatus != 2 && w.FStatus != 3)
            .CountAsync();

        return ApiResult<QualitySummaryDto>.Success(new QualitySummaryDto
        {
            PendingTotal = pendingTotal,
            TodayNew = todayNew,
            OverdueWarning = overdueWarning
        });
    }
}

public class QualitySummaryDto
{
    public int PendingTotal { get; set; }
    public int TodayNew { get; set; }
    public int OverdueWarning { get; set; }
}
