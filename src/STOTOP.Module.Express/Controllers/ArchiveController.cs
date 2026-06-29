using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Services;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.Express.Controllers;

/// <summary>
/// 运单归档管理
/// </summary>
[Authorize]
[ApiController]
[Route("api/express/archive")]
public class ArchiveController : ControllerBase
{
    private readonly IWaybillArchiveService _archiveService;

    public ArchiveController(IWaybillArchiveService archiveService)
    {
        _archiveService = archiveService;
    }

    /// <summary>
    /// 手动触发归档
    /// </summary>
    [HttpPost("execute")]
    [RequirePermission(ExpressPermissions.ArchiveExecute)]
    public async Task<ApiResult<ArchiveResultDto>> Execute()
    {
        try
        {
            var result = await _archiveService.ExecuteArchiveAsync();
            return ApiResult<ArchiveResultDto>.Success(result, "归档执行完成");
        }
        catch (Exception ex)
        {
            return ApiResult<ArchiveResultDto>.Fail($"归档执行失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取归档统计
    /// </summary>
    [HttpGet("stats")]
    [RequirePermission(ExpressPermissions.ArchiveView)]
    public async Task<ApiResult<ArchiveStatsDto>> GetStats()
    {
        var result = await _archiveService.GetArchiveStatsAsync();
        return ApiResult<ArchiveStatsDto>.Success(result);
    }
}
