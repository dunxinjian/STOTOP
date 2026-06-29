using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.CRM.Dtos;
using STOTOP.Module.CRM.Services.Interfaces;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.CRM.Controllers;

[Authorize]
[ApiController]
[Route("api/crm/bonus")]
public class BonusController : ControllerBase
{
    private readonly IBonusService _service;

    public BonusController(IBonusService service)
    {
        _service = service;
    }

    [HttpGet("plans")]
    [RequirePermission(CrmPermissions.BonusView)]
    public async Task<ApiResult<PagedResult<BonusPlanDto>>> GetPlans([FromQuery] BonusPlanQueryRequest request)
    {
        var result = await _service.GetBonusPlansAsync(request);
        return ApiResult<PagedResult<BonusPlanDto>>.Success(result);
    }

    [HttpGet("plans/{id}")]
    [RequirePermission(CrmPermissions.BonusView)]
    public async Task<ApiResult<BonusPlanDto>> GetPlanById(long id)
    {
        var result = await _service.GetBonusPlanByIdAsync(id);
        if (result == null)
            return ApiResult<BonusPlanDto>.Fail("奖金方案不存在");
        return ApiResult<BonusPlanDto>.Success(result);
    }

    [HttpPost("plans")]
    [RequirePermission(CrmPermissions.BonusManage)]
    public async Task<ApiResult<BonusPlanDto>> CreatePlan([FromBody] CreateBonusPlanRequest request)
    {
        var result = await _service.CreateBonusPlanAsync(request);
        return ApiResult<BonusPlanDto>.Success(result, "创建奖金方案成功");
    }

    [HttpPut("plans/{id}")]
    [RequirePermission(CrmPermissions.BonusManage)]
    public async Task<ApiResult<BonusPlanDto>> UpdatePlan(long id, [FromBody] UpdateBonusPlanRequest request)
    {
        try
        {
            var result = await _service.UpdateBonusPlanAsync(id, request);
            if (result == null)
                return ApiResult<BonusPlanDto>.Fail("奖金方案不存在");
            return ApiResult<BonusPlanDto>.Success(result, "更新奖金方案成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<BonusPlanDto>.Fail(ex.Message);
        }
    }

    [HttpDelete("plans/{id}")]
    [RequirePermission(CrmPermissions.BonusManage)]
    public async Task<ApiResult> DeletePlan(long id)
    {
        try
        {
            var result = await _service.DeleteBonusPlanAsync(id);
            if (!result)
                return ApiResult.Fail("奖金方案不存在");
            return ApiResult.Ok("删除奖金方案成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    [HttpPut("plans/{id}/status")]
    [RequirePermission(CrmPermissions.BonusManage)]
    public async Task<ApiResult> UpdatePlanStatus(long id, [FromBody] UpdateStatusRequest request)
    {
        var result = await _service.UpdatePlanStatusAsync(id, request.Status);
        if (!result)
            return ApiResult.Fail("奖金方案不存在");
        return ApiResult.Ok("更新状态成功");
    }

    [HttpGet("details")]
    [RequirePermission(CrmPermissions.BonusView)]
    public async Task<ApiResult<PagedResult<BonusDetailDto>>> GetDetails([FromQuery] BonusDetailQueryRequest request)
    {
        var result = await _service.GetBonusDetailsAsync(request);
        return ApiResult<PagedResult<BonusDetailDto>>.Success(result);
    }

    [HttpPost("plans/{planId}/details")]
    [RequirePermission(CrmPermissions.BonusManage)]
    public async Task<ApiResult<BonusDetailDto>> AddDetail(long planId, [FromBody] CreateBonusDetailRequest request)
    {
        try
        {
            var result = await _service.AddBonusDetailAsync(planId, request);
            return ApiResult<BonusDetailDto>.Success(result, "添加奖金明细成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<BonusDetailDto>.Fail(ex.Message);
        }
    }

    [HttpDelete("details/{detailId}")]
    [RequirePermission(CrmPermissions.BonusManage)]
    public async Task<ApiResult> DeleteDetail(long detailId)
    {
        try
        {
            var result = await _service.DeleteBonusDetailAsync(detailId);
            if (!result)
                return ApiResult.Fail("奖金明细不存在");
            return ApiResult.Ok("删除奖金明细成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }
}
