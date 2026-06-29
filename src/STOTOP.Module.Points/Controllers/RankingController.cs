using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Points.Dtos;
using STOTOP.Module.Points.Services;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.Points.Controllers;

[Authorize]
[ApiController]
[Route("api/points/rankings")]
public class RankingController : ControllerBase
{
    private readonly IRankingService _service;

    public RankingController(IRankingService service)
    {
        _service = service;
    }

    private long GetUserId() => long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
    private long GetOrgId() => (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);

    /// <summary>排行榜</summary>
    [HttpGet]
    [RequirePermission(PointsPermissions.RankingView)]
    public async Task<ApiResult<PagedResult<RankingListDto>>> GetRankings([FromQuery] RankingPagedRequest request)
    {
        return await _service.GetRankingsAsync(GetOrgId(), request);
    }

    /// <summary>部门排名</summary>
    [HttpGet("department")]
    [RequirePermission(PointsPermissions.RankingView)]
    public async Task<ApiResult<List<DepartmentRankingDto>>> GetDepartmentRankings([FromQuery] DepartmentRankingRequest request)
    {
        return await _service.GetDepartmentRankingsAsync(GetOrgId(), request);
    }

    /// <summary>我的排名</summary>
    [HttpGet("my")]
    [RequirePermission(PointsPermissions.RankingView)]
    public async Task<ApiResult<MyRankingDto>> GetMyRanking([FromQuery] int dimension = 0, [FromQuery] string? period = null)
    {
        return await _service.GetMyRankingAsync(GetOrgId(), GetUserId(), dimension, period);
    }
}
