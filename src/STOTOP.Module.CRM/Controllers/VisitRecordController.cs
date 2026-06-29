using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.CRM.Dtos;
using STOTOP.Module.CRM.Services.Interfaces;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.CRM.Controllers;

[Authorize]
[ApiController]
[Route("api/crm/visits")]
public class VisitRecordController : ControllerBase
{
    private readonly IVisitRecordService _visitService;

    public VisitRecordController(IVisitRecordService visitService)
    {
        _visitService = visitService;
    }

    [HttpGet]
    [RequirePermission(CrmPermissions.VisitView)]
    public async Task<ApiResult<PagedResult<VisitRecordListItemDto>>> GetList([FromQuery] VisitRecordQueryRequest request)
    {
        var result = await _visitService.GetVisitRecordsAsync(request);
        return ApiResult<PagedResult<VisitRecordListItemDto>>.Success(result);
    }

    [HttpGet("{id}")]
    [RequirePermission(CrmPermissions.VisitView)]
    public async Task<ApiResult<VisitRecordDto>> GetById(long id)
    {
        var result = await _visitService.GetVisitRecordByIdAsync(id);
        if (result == null)
            return ApiResult<VisitRecordDto>.Fail("拜访记录不存在");
        return ApiResult<VisitRecordDto>.Success(result);
    }

    [HttpPost]
    [RequirePermission(CrmPermissions.VisitCreate)]
    public async Task<ApiResult<VisitRecordDto>> Create([FromBody] CreateVisitRecordRequest request)
    {
        try
        {
            var result = await _visitService.CreateVisitRecordAsync(request);
            return ApiResult<VisitRecordDto>.Success(result, "创建拜访记录成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<VisitRecordDto>.Fail(ex.Message);
        }
    }

    [HttpPut("{id}")]
    [RequirePermission(CrmPermissions.VisitEdit)]
    public async Task<ApiResult<VisitRecordDto>> Update(long id, [FromBody] UpdateVisitRecordRequest request)
    {
        var result = await _visitService.UpdateVisitRecordAsync(id, request);
        if (result == null)
            return ApiResult<VisitRecordDto>.Fail("拜访记录不存在");
        return ApiResult<VisitRecordDto>.Success(result, "更新拜访记录成功");
    }

    [HttpDelete("{id}")]
    [RequirePermission(CrmPermissions.VisitEdit)]
    public async Task<ApiResult> Delete(long id)
    {
        var result = await _visitService.DeleteVisitRecordAsync(id);
        if (!result)
            return ApiResult.Fail("拜访记录不存在");
        return ApiResult.Ok("删除拜访记录成功");
    }

    [HttpGet("pending-follow-up")]
    [RequirePermission(CrmPermissions.VisitView)]
    public async Task<ApiResult<List<VisitRecordListItemDto>>> GetPendingFollowUp([FromQuery] long? visitorId)
    {
        var result = await _visitService.GetPendingFollowUpAsync(visitorId);
        return ApiResult<List<VisitRecordListItemDto>>.Success(result);
    }

    [HttpGet("statistics")]
    [RequirePermission(CrmPermissions.VisitView)]
    public async Task<ApiResult<VisitStatisticsDto>> GetStatistics([FromQuery] long? visitorId, [FromQuery] long? orgId)
    {
        var result = await _visitService.GetStatisticsAsync(visitorId, orgId);
        return ApiResult<VisitStatisticsDto>.Success(result);
    }
}
