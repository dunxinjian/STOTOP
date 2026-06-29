using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.System.Filters;
using STOTOP.Module.Quality.Dtos;
using STOTOP.Module.Quality.Services.Review;

namespace STOTOP.Module.Quality.Controllers;

[Authorize]
[ApiController]
[Route("api/quality")]
public class ReviewController : ControllerBase
{
    private readonly IReviewService _service;

    public ReviewController(IReviewService service)
    {
        _service = service;
    }

    private long GetUserId() => long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
    private long GetOrgId() => (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);

    /// <summary>复盘分页列表</summary>
    [HttpGet("reviews")]
    [RequirePermission(QualityPermissions.ReviewView)]
    public async Task<ApiResult<PagedResult<ReviewDto>>> GetPaged([FromQuery] ReviewPagedRequest request)
    {
        return await _service.GetPagedAsync(GetOrgId(), request);
    }

    /// <summary>复盘详情</summary>
    [HttpGet("reviews/{id}")]
    [RequirePermission(QualityPermissions.ReviewView)]
    public async Task<ApiResult<ReviewDto>> GetById(long id)
    {
        return await _service.GetByIdAsync(GetOrgId(), id);
    }

    /// <summary>创建复盘</summary>
    [HttpPost("reviews")]
    [RequirePermission(QualityPermissions.ReviewManage)]
    public async Task<ApiResult<ReviewDto>> Create([FromBody] CreateReviewRequest request)
    {
        return await _service.CreateAsync(GetOrgId(), GetUserId(), request);
    }

    /// <summary>更新复盘</summary>
    [HttpPut("reviews/{id}")]
    [RequirePermission(QualityPermissions.ReviewManage)]
    public async Task<ApiResult<ReviewDto>> Update(long id, [FromBody] UpdateReviewRequest request)
    {
        return await _service.UpdateAsync(GetOrgId(), GetUserId(), id, request);
    }

    /// <summary>删除复盘</summary>
    [HttpDelete("reviews/{id}")]
    [RequirePermission(QualityPermissions.ReviewManage)]
    public async Task<ApiResult<bool>> Delete(long id)
    {
        return await _service.DeleteAsync(GetOrgId(), id);
    }

    /// <summary>复盘统计</summary>
    [HttpGet("reviews/stats")]
    [RequirePermission(QualityPermissions.ReviewView)]
    public async Task<ApiResult<ReviewStatsDto>> GetStats()
    {
        return await _service.GetStatsAsync(GetOrgId());
    }

    /// <summary>改进措施分页列表</summary>
    [HttpGet("improvements")]
    [RequirePermission(QualityPermissions.ReviewView)]
    public async Task<ApiResult<PagedResult<ImprovementListDto>>> GetImprovements([FromQuery] ImprovementPagedRequest request)
    {
        return await _service.GetImprovementsAsync(GetOrgId(), request);
    }

    /// <summary>更新改进措施</summary>
    [HttpPut("improvements/{id}")]
    [RequirePermission(QualityPermissions.ReviewManage)]
    public async Task<ApiResult<bool>> UpdateImprovement(long id, [FromBody] UpdateImprovementRequest request)
    {
        return await _service.UpdateImprovementAsync(GetOrgId(), id, request);
    }

    /// <summary>完成改进措施</summary>
    [HttpPost("improvements/{id}/complete")]
    [RequirePermission(QualityPermissions.ReviewManage)]
    public async Task<ApiResult<bool>> CompleteImprovement(long id, [FromBody] CompleteImprovementRequest request)
    {
        return await _service.CompleteImprovementAsync(GetOrgId(), id, request);
    }
}
