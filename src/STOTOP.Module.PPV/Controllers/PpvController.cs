using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.System.Filters;
using STOTOP.Module.PPV.Dtos;
using STOTOP.Module.PPV.Services;

namespace STOTOP.Module.PPV.Controllers;

[Authorize]
[ApiController]
[Route("api/ppv")]
public class PpvController : ControllerBase
{
    private readonly IPpvTemplateService _templateService;
    private readonly IPpvRecordService _recordService;
    private readonly IPpvResultService _resultService;

    public PpvController(
        IPpvTemplateService templateService,
        IPpvRecordService recordService,
        IPpvResultService resultService)
    {
        _templateService = templateService;
        _recordService = recordService;
        _resultService = resultService;
    }

    private long GetUserId() => long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
    private long GetOrgId() => (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);

    // ===== 模板管理 =====

    /// <summary>模板列表</summary>
    [HttpGet("templates")]
    [RequirePermission(PpvPermissions.TemplateView)]
    public Task<ApiResult<List<PpvTemplateDto>>> GetTemplates(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] long? positionId = null)
        => _templateService.GetListAsync(GetOrgId(), page, pageSize, positionId);

    /// <summary>创建模板</summary>
    [HttpPost("templates")]
    [RequirePermission(PpvPermissions.TemplateEdit)]
    public Task<ApiResult<PpvTemplateDto>> CreateTemplate([FromBody] CreatePpvTemplateRequest request)
        => _templateService.CreateAsync(GetOrgId(), request);

    /// <summary>更新模板</summary>
    [HttpPut("templates/{id:long}")]
    [RequirePermission(PpvPermissions.TemplateEdit)]
    public Task<ApiResult<PpvTemplateDto>> UpdateTemplate(long id, [FromBody] UpdatePpvTemplateRequest request)
        => _templateService.UpdateAsync(GetOrgId(), id, request);

    /// <summary>切换启用状态</summary>
    [HttpPost("templates/{id:long}/enable")]
    [RequirePermission(PpvPermissions.TemplateEdit)]
    public Task<ApiResult> EnableTemplate(long id)
        => _templateService.EnableAsync(GetOrgId(), id);

    // ===== 产值记录 =====

    /// <summary>记录列表</summary>
    [HttpGet("records")]
    [RequirePermission(PpvPermissions.RecordView)]
    public Task<ApiResult<List<PpvRecordDto>>> GetRecords([FromQuery] PpvRecordPagedRequest request)
        => _recordService.GetListAsync(GetOrgId(), request);

    /// <summary>创建记录</summary>
    [HttpPost("records")]
    [RequirePermission(PpvPermissions.RecordEdit)]
    public Task<ApiResult<PpvRecordDto>> CreateRecord([FromBody] CreatePpvRecordRequest request)
        => _recordService.CreateAsync(GetOrgId(), GetUserId(), request);

    /// <summary>更新记录</summary>
    [HttpPut("records/{id:long}")]
    [RequirePermission(PpvPermissions.RecordEdit)]
    public Task<ApiResult<PpvRecordDto>> UpdateRecord(long id, [FromBody] UpdatePpvRecordRequest request)
        => _recordService.UpdateAsync(GetOrgId(), id, request);

    /// <summary>审核记录</summary>
    [HttpPost("records/{id:long}/review")]
    [RequirePermission(PpvPermissions.RecordReview)]
    public Task<ApiResult> ReviewRecord(long id, [FromBody] ReviewPpvRecordRequest request)
        => _recordService.ReviewAsync(GetOrgId(), id, GetUserId(), request);

    /// <summary>我的记录</summary>
    [HttpGet("records/my")]
    [Authorize]
    public Task<ApiResult<List<PpvRecordDto>>> GetMyRecords([FromQuery] string? period = null)
        => _recordService.GetMyRecordsAsync(GetOrgId(), GetUserId(), period);

    // ===== 月度汇总 =====

    /// <summary>月度汇总列表</summary>
    [HttpGet("results")]
    [RequirePermission(PpvPermissions.ResultView)]
    public Task<ApiResult<List<PpvMonthlyResultDto>>> GetResults(
        [FromQuery] string? period = null,
        [FromQuery] long? employeeId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
        => _resultService.GetListAsync(GetOrgId(), period, employeeId, page, pageSize);

    /// <summary>月度汇总详情</summary>
    [HttpGet("results/{id:long}")]
    [RequirePermission(PpvPermissions.ResultView)]
    public Task<ApiResult<PpvMonthlyResultDto>> GetResultDetail(long id)
        => _resultService.GetDetailAsync(GetOrgId(), id);

    /// <summary>我的月度汇总</summary>
    [HttpGet("results/my")]
    [Authorize]
    public Task<ApiResult<List<PpvMonthlyResultDto>>> GetMyResults([FromQuery] string? period = null)
        => _resultService.GetMyResultsAsync(GetOrgId(), GetUserId(), period);
}
