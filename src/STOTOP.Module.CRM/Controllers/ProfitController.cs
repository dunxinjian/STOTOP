using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.CRM.Dtos;
using STOTOP.Module.CRM.Services.Interfaces;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.CRM.Controllers;

[Authorize]
[ApiController]
[Route("api/crm/profits")]
public class ProfitController : ControllerBase
{
    private readonly IProfitCalcService _service;

    public ProfitController(IProfitCalcService service)
    {
        _service = service;
    }

    [HttpGet]
    [RequirePermission(CrmPermissions.ProfitView)]
    public async Task<ApiResult<PagedResult<CustomerProfitDto>>> GetList([FromQuery] ProfitQueryRequest request)
    {
        var result = await _service.GetProfitsAsync(request);
        return ApiResult<PagedResult<CustomerProfitDto>>.Success(result);
    }

    [HttpGet("{id}")]
    [RequirePermission(CrmPermissions.ProfitView)]
    public async Task<ApiResult<CustomerProfitDto>> GetById(long id)
    {
        var result = await _service.GetProfitByIdAsync(id);
        if (result == null)
            return ApiResult<CustomerProfitDto>.Fail("毛利记录不存在");
        return ApiResult<CustomerProfitDto>.Success(result);
    }

    [HttpPost]
    [RequirePermission(CrmPermissions.ProfitCalc)]
    public async Task<ApiResult<CustomerProfitDto>> Create([FromBody] CreateProfitRequest request)
    {
        var result = await _service.CreateProfitAsync(request);
        return ApiResult<CustomerProfitDto>.Success(result, "创建毛利记录成功");
    }

    [HttpPut("{id}")]
    [RequirePermission(CrmPermissions.ProfitCalc)]
    public async Task<ApiResult<CustomerProfitDto>> Update(long id, [FromBody] CreateProfitRequest request)
    {
        var result = await _service.UpdateProfitAsync(id, request);
        if (result == null)
            return ApiResult<CustomerProfitDto>.Fail("毛利记录不存在");
        return ApiResult<CustomerProfitDto>.Success(result, "更新毛利记录成功");
    }

    [HttpDelete("{id}")]
    [RequirePermission(CrmPermissions.ProfitCalc)]
    public async Task<ApiResult> Delete(long id)
    {
        var result = await _service.DeleteProfitAsync(id);
        if (!result)
            return ApiResult.Fail("毛利记录不存在");
        return ApiResult.Ok("删除毛利记录成功");
    }

    [HttpGet("summary")]
    [RequirePermission(CrmPermissions.ProfitView)]
    public async Task<ApiResult<List<ProfitSummaryDto>>> GetSummary([FromQuery] long? orgId, [FromQuery] string? period)
    {
        var result = await _service.GetProfitSummaryAsync(orgId, period);
        return ApiResult<List<ProfitSummaryDto>>.Success(result);
    }

    [HttpGet("ranking")]
    [RequirePermission(CrmPermissions.ProfitView)]
    public async Task<ApiResult<List<ProfitRankingDto>>> GetRanking(
        [FromQuery] long? orgId, [FromQuery] string? period, [FromQuery] int top = 20)
    {
        var result = await _service.GetProfitRankingAsync(orgId, period, top);
        return ApiResult<List<ProfitRankingDto>>.Success(result);
    }
}
