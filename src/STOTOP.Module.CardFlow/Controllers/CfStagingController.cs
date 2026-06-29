using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Models;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Services.Staging;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.CardFlow.Controllers;

[Authorize]
[ApiController]
[Route("api/cardflow/staging")]
public class CfStagingController : ControllerBase
{
    private readonly StagingService _stagingService;
    private readonly ILogger<CfStagingController> _logger;

    public CfStagingController(
        StagingService stagingService,
        ILogger<CfStagingController> logger)
    {
        _stagingService = stagingService;
        _logger = logger;
    }

    /// <summary>查询暂存表数据（分页+筛选）</summary>
    [HttpGet("{targetTable}")]
    [RequirePermission(CardFlowPermissions.Staging)]
    public async Task<ApiResult<PagedResult<Dictionary<string, object?>>>> GetStagingData(
        string targetTable, [FromQuery] StagingQueryFilter filter)
    {
        try
        {
            var result = await _stagingService.GetStagingDataAsync(targetTable, filter);
            return ApiResult<PagedResult<Dictionary<string, object?>>>.Success(result);
        }
        catch (ArgumentException ex)
        {
            return ApiResult<PagedResult<Dictionary<string, object?>>>.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查询暂存数据失败: {TargetTable}", targetTable);
            return ApiResult<PagedResult<Dictionary<string, object?>>>.Fail($"查询失败：{ex.Message}", 500);
        }
    }

    /// <summary>单条记录详情</summary>
    [HttpGet("{targetTable}/{id:long}")]
    [RequirePermission(CardFlowPermissions.Staging)]
    public async Task<ApiResult<Dictionary<string, object?>>> GetRecord(string targetTable, long id)
    {
        try
        {
            var result = await _stagingService.GetRecordAsync(targetTable, id);
            if (result == null)
                return ApiResult<Dictionary<string, object?>>.Fail("记录不存在");
            return ApiResult<Dictionary<string, object?>>.Success(result);
        }
        catch (ArgumentException ex)
        {
            return ApiResult<Dictionary<string, object?>>.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取记录失败: {TargetTable}/{Id}", targetTable, id);
            return ApiResult<Dictionary<string, object?>>.Fail($"获取失败：{ex.Message}", 500);
        }
    }

    /// <summary>编辑单条记录</summary>
    [HttpPut("{targetTable}/{id:long}")]
    [RequirePermission(CardFlowPermissions.Staging)]
    public async Task<ApiResult> UpdateRecord(
        string targetTable, long id, [FromBody] Dictionary<string, object?> fields)
    {
        try
        {
            var success = await _stagingService.UpdateRecordAsync(targetTable, id, fields);
            if (!success)
                return ApiResult.Fail("记录不存在");
            return ApiResult.Ok("更新成功");
        }
        catch (ArgumentException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新记录失败: {TargetTable}/{Id}", targetTable, id);
            return ApiResult.Fail($"更新失败：{ex.Message}", 500);
        }
    }

    /// <summary>批量删除</summary>
    [HttpDelete("{targetTable}")]
    [RequirePermission(CardFlowPermissions.Staging)]
    public async Task<ApiResult<int>> BatchDelete(
        string targetTable, [FromBody] List<long> ids)
    {
        try
        {
            var count = await _stagingService.BatchDeleteAsync(targetTable, ids);
            return ApiResult<int>.Success(count, $"成功删除 {count} 条记录");
        }
        catch (ArgumentException ex)
        {
            return ApiResult<int>.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量删除失败: {TargetTable}", targetTable);
            return ApiResult<int>.Fail($"删除失败：{ex.Message}", 500);
        }
    }

    /// <summary>批量修改处理状态</summary>
    [HttpPost("{targetTable}/batch-update-status")]
    [RequirePermission(CardFlowPermissions.Staging)]
    public async Task<ApiResult<int>> BatchUpdateStatus(
        string targetTable, [FromBody] BatchUpdateStatusRequest request)
    {
        try
        {
            var count = await _stagingService.BatchUpdateStatusAsync(targetTable, request.Ids, request.NewStatus);
            return ApiResult<int>.Success(count, $"成功更新 {count} 条记录状态");
        }
        catch (ArgumentException ex)
        {
            return ApiResult<int>.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量更新状态失败: {TargetTable}", targetTable);
            return ApiResult<int>.Fail($"更新状态失败：{ex.Message}", 500);
        }
    }

    /// <summary>暂存表统计</summary>
    [HttpGet("{targetTable}/stats")]
    [RequirePermission(CardFlowPermissions.Staging)]
    public async Task<ApiResult<StagingStatsDto>> GetStats(string targetTable)
    {
        try
        {
            var result = await _stagingService.GetStatsAsync(targetTable);
            return ApiResult<StagingStatsDto>.Success(result);
        }
        catch (ArgumentException ex)
        {
            return ApiResult<StagingStatsDto>.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取统计失败: {TargetTable}", targetTable);
            return ApiResult<StagingStatsDto>.Fail($"获取统计失败：{ex.Message}", 500);
        }
    }
}
