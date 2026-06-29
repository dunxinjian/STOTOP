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
[Route("api/points")]
public class PointController : ControllerBase
{
    private readonly IPointService _service;

    public PointController(IPointService service)
    {
        _service = service;
    }

    private long GetUserId() => long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
    private long GetOrgId() => (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);

    /// <summary>手动奖分</summary>
    [HttpPost("award")]
    [RequirePermission(PointsPermissions.Award)]
    public async Task<ApiResult<PointRecordListDto>> Award([FromBody] ManualAwardRequest request)
    {
        return await _service.AwardAsync(GetOrgId(), GetUserId(), request);
    }

    /// <summary>手动扣分</summary>
    [HttpPost("deduct")]
    [RequirePermission(PointsPermissions.Deduct)]
    public async Task<ApiResult<PointRecordListDto>> Deduct([FromBody] ManualDeductRequest request)
    {
        return await _service.DeductAsync(GetOrgId(), GetUserId(), request);
    }

    /// <summary>事件触发积分</summary>
    [HttpPost("trigger")]
    [RequirePermission(PointsPermissions.Award)]
    public async Task<ApiResult<bool>> Trigger([FromBody] PointEventDto eventDto)
    {
        return await _service.TriggerEventAsync(eventDto);
    }

    /// <summary>积分流水列表</summary>
    [HttpGet("records")]
    [RequirePermission(PointsPermissions.RecordView)]
    public async Task<ApiResult<PagedResult<PointRecordListDto>>> GetRecords([FromQuery] PointRecordPagedRequest request)
    {
        return await _service.GetRecordsPagedAsync(GetOrgId(), request);
    }

    /// <summary>我的积分明细</summary>
    [HttpGet("records/my")]
    [RequirePermission(PointsPermissions.RecordView)]
    public async Task<ApiResult<PagedResult<PointRecordListDto>>> GetMyRecords([FromQuery] PointRecordPagedRequest request)
    {
        return await _service.GetMyRecordsAsync(GetOrgId(), GetUserId(), request);
    }

    /// <summary>查询账户（聚合 A+B）</summary>
    [HttpGet("account")]
    [RequirePermission(PointsPermissions.RecordView)]
    public async Task<ApiResult<PointAccountDto>> GetAccount([FromQuery] long userId)
    {
        return await _service.GetAccountAsync(GetOrgId(), userId);
    }

    /// <summary>按账户类型查询单一账户行（accountType: 1=A / 2=B）</summary>
    [HttpGet("account/by-type")]
    [RequirePermission(PointsPermissions.RecordView)]
    public async Task<ApiResult<PointAccountDto>> GetAccountByType([FromQuery] int accountType, [FromQuery] long? userId = null)
    {
        var targetUserId = userId.GetValueOrDefault(0L);
        if (targetUserId <= 0) targetUserId = GetUserId();
        return await _service.GetAccountByTypeAsync(GetOrgId(), targetUserId, accountType);
    }

    /// <summary>按指定日期计算账户余额（半开区间，清算 Job / 历史回查使用）</summary>
    [HttpGet("balance-at-date")]
    [RequirePermission(PointsPermissions.RecordView)]
    public async Task<ApiResult<HistoricalBalanceDto>> GetBalanceAtDate([FromQuery] int accountType, [FromQuery] DateTime atDate, [FromQuery] long? userId = null)
    {
        var targetUserId = userId.GetValueOrDefault(0L);
        if (targetUserId <= 0) targetUserId = GetUserId();

        var balanceResult = await _service.GetAccountBalanceAtDateAsync(GetOrgId(), targetUserId, accountType, atDate);
        if (balanceResult.Code != 200)
            return ApiResult<HistoricalBalanceDto>.Fail(string.IsNullOrEmpty(balanceResult.Message) ? "查询失败" : balanceResult.Message, balanceResult.Code);

        return ApiResult<HistoricalBalanceDto>.Success(new HistoricalBalanceDto
        {
            UserId = targetUserId,
            AccountType = accountType,
            AtDate = atDate,
            Balance = balanceResult.Data
        });
    }

    /// <summary>我的账户（不传 accountType 返回 A+B 聚合；传 1/2 返回单一账户）</summary>
    [HttpGet("account/my")]
    [RequirePermission(PointsPermissions.RecordView)]
    public async Task<ApiResult<PointAccountDto>> GetMyAccount([FromQuery] int? accountType = null)
    {
        if (accountType.HasValue)
        {
            return await _service.GetAccountByTypeAsync(GetOrgId(), GetUserId(), accountType.Value);
        }
        return await _service.GetMyAccountAsync(GetOrgId(), GetUserId());
    }

    /// <summary>统计看板</summary>
    [HttpGet("statistics")]
    [RequirePermission(PointsPermissions.DashboardView)]
    public async Task<ApiResult<PointStatisticsDto>> GetStatistics()
    {
        return await _service.GetStatisticsAsync(GetOrgId(), GetUserId());
    }
}
