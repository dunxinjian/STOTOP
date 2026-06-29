using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Services.Interfaces;

namespace STOTOP.Module.CardFlow.Controllers;

[Authorize]
[ApiController]
[Route("api/cardflow/issues")]
public class CfProcessingIssueController : ControllerBase
{
    private readonly IProcessingIssueService _issueService;

    public CfProcessingIssueController(IProcessingIssueService issueService)
    {
        _issueService = issueService;
    }

    [HttpGet]
    public async Task<ApiResult<PagedResult<ProcessingIssueDto>>> GetList([FromQuery] ProcessingIssueQueryRequest request)
    {
        var result = await _issueService.GetIssuesAsync(request, GetCurrentOrgId());
        return ApiResult<PagedResult<ProcessingIssueDto>>.Success(result);
    }

    [HttpGet("{id:long}")]
    public async Task<ApiResult<ProcessingIssueDto>> GetById(long id)
    {
        var issue = await _issueService.GetIssueAsync(id, GetCurrentOrgId());
        return issue == null
            ? ApiResult<ProcessingIssueDto>.Fail("异常记录不存在", 404)
            : ApiResult<ProcessingIssueDto>.Success(issue);
    }

    [HttpPost("report")]
    public async Task<ApiResult<ProcessingIssueDto>> Report([FromBody] ProcessingIssueReportRequest request)
    {
        var entity = await _issueService.ReportAsync(request, GetCurrentOrgId());
        var dto = await _issueService.GetIssueAsync(entity.FID, GetCurrentOrgId());
        return ApiResult<ProcessingIssueDto>.Success(dto!, "异常已记录");
    }

    [HttpPost("dispatch")]
    public async Task<ApiResult> DispatchBatch([FromQuery] long batchId)
    {
        if (batchId <= 0)
            return ApiResult.Fail("批次ID不能为空");

        await _issueService.DispatchBatchAsync(batchId, GetCurrentOrgId());
        return ApiResult.Ok("异常派发已触发");
    }

    [HttpPost("{id:long}/resolve")]
    public async Task<ApiResult> Resolve(long id, [FromBody] ProcessingIssueResolveRequest request)
    {
        await _issueService.ResolveAsync(id, request, GetCurrentUserId(), GetCurrentOrgId());
        return ApiResult.Ok("异常已处理");
    }

    [HttpPost("{id:long}/ignore")]
    public async Task<ApiResult> Ignore(long id, [FromBody] ProcessingIssueResolveRequest request)
    {
        await _issueService.IgnoreAsync(id, request, GetCurrentUserId(), GetCurrentOrgId());
        return ApiResult.Ok("异常已忽略");
    }

    [HttpPost("{id:long}/retry")]
    public async Task<ApiResult> Retry(long id, [FromBody] ProcessingIssueRetryRequest request)
    {
        await _issueService.RetryAsync(id, request, GetCurrentUserId(), GetCurrentOrgId());
        return ApiResult.Ok("重跑请求已记录");
    }

    private long GetCurrentOrgId() => (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);

    private long GetCurrentUserId()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return !string.IsNullOrEmpty(userIdStr) && long.TryParse(userIdStr, out var userId)
            ? userId
            : 0;
    }
}

