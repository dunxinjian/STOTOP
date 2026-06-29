using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.System.Dtos;
using STOTOP.Module.System.Services.Interfaces;

namespace STOTOP.Module.System.Controllers;

[ApiController]
[Route("api/system/schema-sync")]
[Authorize]
public class SchemaSyncController : ControllerBase
{
    private readonly ISchemaSyncManageService _service;

    public SchemaSyncController(ISchemaSyncManageService service)
    {
        _service = service;
    }

    /// <summary>
    /// 获取当前同步状态
    /// </summary>
    [HttpGet("status")]
    public async Task<ApiResult<SchemaSyncStatusDto>> GetStatus()
    {
        var result = await _service.GetStatusAsync();
        return ApiResult<SchemaSyncStatusDto>.Success(result);
    }

    /// <summary>
    /// 获取待执行变更列表
    /// </summary>
    [HttpGet("pending")]
    public async Task<ApiResult<List<SchemaChangeItemDto>>> GetPending()
    {
        var result = await _service.GetPendingChangesAsync();
        return ApiResult<List<SchemaChangeItemDto>>.Success(result);
    }

    /// <summary>
    /// 执行选中的变更
    /// </summary>
    [HttpPost("execute")]
    public async Task<ApiResult> Execute([FromBody] ExecuteChangesRequest request)
    {
        var userName = User.FindFirst("userName")?.Value ?? User.Identity?.Name;
        await _service.ExecuteChangesAsync(request.ChangeIds, userName);
        return ApiResult.Ok("执行成功");
    }

    /// <summary>
    /// 跳过选中的变更
    /// </summary>
    [HttpPost("skip")]
    public async Task<ApiResult> Skip([FromBody] SkipChangesRequest request)
    {
        await _service.SkipChangesAsync(request.ChangeIds);
        return ApiResult.Ok("已跳过");
    }

    /// <summary>
    /// 获取警告列表
    /// </summary>
    [HttpGet("warnings")]
    public async Task<ApiResult<List<SchemaWarningItemDto>>> GetWarnings()
    {
        var result = await _service.GetWarningsAsync();
        return ApiResult<List<SchemaWarningItemDto>>.Success(result);
    }

    /// <summary>
    /// 获取执行历史（分页）
    /// </summary>
    [HttpGet("history")]
    public async Task<ApiResult<object>> GetHistory([FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 15)
    {
        var (items, total) = await _service.GetHistoryAsync(pageIndex, pageSize);
        return ApiResult<object>.Success(new { items, total });
    }
}
