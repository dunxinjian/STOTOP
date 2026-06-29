using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.CRM.Dtos;
using STOTOP.Module.CRM.Services.Interfaces;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.CRM.Controllers;

[Authorize]
[ApiController]
[Route("api/crm/referrals")]
public class ReferralController : ControllerBase
{
    private readonly IReferralCommissionService _service;

    public ReferralController(IReferralCommissionService service)
    {
        _service = service;
    }

    [HttpGet]
    [RequirePermission(CrmPermissions.ReferralView)]
    public async Task<ApiResult<PagedResult<ReferralDto>>> GetList([FromQuery] ReferralQueryRequest request)
    {
        var result = await _service.GetReferralsAsync(request);
        return ApiResult<PagedResult<ReferralDto>>.Success(result);
    }

    [HttpGet("{id}")]
    [RequirePermission(CrmPermissions.ReferralView)]
    public async Task<ApiResult<ReferralDto>> GetById(long id)
    {
        var result = await _service.GetReferralByIdAsync(id);
        if (result == null)
            return ApiResult<ReferralDto>.Fail("推荐记录不存在");
        return ApiResult<ReferralDto>.Success(result);
    }

    [HttpPost]
    [RequirePermission(CrmPermissions.ReferralCreate)]
    public async Task<ApiResult<ReferralDto>> Create([FromBody] CreateReferralRequest request)
    {
        var result = await _service.CreateReferralAsync(request);
        return ApiResult<ReferralDto>.Success(result, "创建推荐记录成功");
    }

    [HttpDelete("{id}")]
    [RequirePermission(CrmPermissions.ReferralCreate)]
    public async Task<ApiResult> Delete(long id)
    {
        var result = await _service.DeleteReferralAsync(id);
        if (!result)
            return ApiResult.Fail("推荐记录不存在");
        return ApiResult.Ok("删除推荐记录成功");
    }

    [HttpGet("statistics")]
    [RequirePermission(CrmPermissions.ReferralView)]
    public async Task<ApiResult<List<ReferralStatisticsDto>>> GetStatistics(
        [FromQuery] long? orgId, [FromQuery] DateOnly? startDate, [FromQuery] DateOnly? endDate)
    {
        var result = await _service.GetStatisticsByReferrerAsync(orgId, startDate, endDate);
        return ApiResult<List<ReferralStatisticsDto>>.Success(result);
    }
}
