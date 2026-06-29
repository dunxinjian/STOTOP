using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Services;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.Express.Controllers;

/// <summary>
/// 政策返利结算管理
/// </summary>
[Authorize]
[ApiController]
[Route("api/express/policy-rebate-settlement")]
public class PolicyRebateSettlementController : ControllerBase
{
    private readonly IPolicyRebateSettlementService _service;
    private readonly PolicyRebateSimulator _simulator;

    public PolicyRebateSettlementController(
        IPolicyRebateSettlementService service,
        PolicyRebateSimulator simulator)
    {
        _service = service;
        _simulator = simulator;
    }

    /// <summary>
    /// 分页查询结算列表
    /// </summary>
    [HttpGet]
    [RequirePermission(ExpressPermissions.PolicyRebateSettlementView)]
    public async Task<ApiResult<PagedResult<PolicyRebateSettlementListItemDto>>> GetList([FromQuery] SettlementQueryRequest request)
    {
        var result = await _service.GetPagedListAsync(request);
        return ApiResult<PagedResult<PolicyRebateSettlementListItemDto>>.Success(result);
    }

    /// <summary>
    /// 结算详情
    /// </summary>
    [HttpGet("{id}")]
    [RequirePermission(ExpressPermissions.PolicyRebateSettlementView)]
    public async Task<ApiResult<PolicyRebateSettlementDetailDto>> GetDetail(long id)
    {
        var result = await _service.GetDetailAsync(id);
        if (result == null)
            return ApiResult<PolicyRebateSettlementDetailDto>.Fail("结算记录不存在");
        return ApiResult<PolicyRebateSettlementDetailDto>.Success(result);
    }

    /// <summary>
    /// 执行结算
    /// </summary>
    [HttpPost("execute")]
    [RequirePermission(ExpressPermissions.PolicyRebateSettlementExecute)]
    public async Task<ApiResult<PolicyRebateSettlementDetailDto>> Execute([FromBody] ExecuteSettlementRequest request)
    {
        try
        {
            var result = await _service.ExecuteSettlementAsync(request.PolicyRebateId, request.PeriodStart, request.PeriodEnd);
            return ApiResult<PolicyRebateSettlementDetailDto>.Success(result, "结算执行成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<PolicyRebateSettlementDetailDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 确认结算
    /// </summary>
    [HttpPut("{id}/confirm")]
    [RequirePermission(ExpressPermissions.PolicyRebateSettlementConfirm)]
    public async Task<ApiResult> Confirm(long id)
    {
        var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? "unknown";
        var result = await _service.ConfirmAsync(id, userName);
        if (!result) return ApiResult.Fail("结算记录不存在或状态不正确");
        return ApiResult.Ok("确认成功");
    }

    /// <summary>
    /// 核销结算
    /// </summary>
    [HttpPut("{id}/write-off")]
    [RequirePermission(ExpressPermissions.PolicyRebateSettlementConfirm)]
    public async Task<ApiResult> WriteOff(long id)
    {
        var result = await _service.WriteOffAsync(id);
        if (!result) return ApiResult.Fail("结算记录不存在或状态不正确");
        return ApiResult.Ok("核销成功");
    }

    /// <summary>
    /// 返利测算
    /// </summary>
    [HttpPost("simulate")]
    [RequirePermission(ExpressPermissions.PolicyRebateSimulate)]
    public async Task<ApiResult<SimulationResult>> Simulate([FromBody] SimulationRequest request)
    {
        try
        {
            var result = await _simulator.SimulateAsync(request);
            return ApiResult<SimulationResult>.Success(result);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<SimulationResult>.Fail(ex.Message);
        }
    }
}
