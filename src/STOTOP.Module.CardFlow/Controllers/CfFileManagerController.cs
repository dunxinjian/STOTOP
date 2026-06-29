using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Models;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Services.FileManager;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.CardFlow.Controllers;

[Authorize]
[ApiController]
[Route("api/cardflow/files")]
public class CfFileManagerController : ControllerBase
{
    private readonly FileManagerService _service;
    private readonly ILogger<CfFileManagerController> _logger;

    public CfFileManagerController(FileManagerService service, ILogger<CfFileManagerController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>分页查询上传文件列表</summary>
    [HttpGet]
    [RequirePermission(CardFlowPermissions.FileManagerView)]
    public async Task<ApiResult<STOTOP.Core.Models.PagedResult<UploadedFileDto>>> GetFiles([FromQuery] FileQueryFilter filter)
    {
        try
        {
            var result = await _service.GetUploadedFilesAsync(filter);
            return ApiResult<PagedResult<UploadedFileDto>>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查询文件列表失败");
            return ApiResult<PagedResult<UploadedFileDto>>.Fail($"查询失败：{ex.Message}", 500);
        }
    }

    /// <summary>获取单个文件详情</summary>
    [HttpGet("{batchId:long}")]
    [RequirePermission(CardFlowPermissions.FileManagerView)]
    public async Task<ApiResult<UploadedFileDto>> GetFileDetail(long batchId)
    {
        var result = await _service.GetFileDetailAsync(batchId);
        if (result == null)
            return ApiResult<UploadedFileDto>.Fail("文件不存在");
        return ApiResult<UploadedFileDto>.Success(result);
    }

    /// <summary>批量删除文件</summary>
    [HttpDelete]
    [RequirePermission(CardFlowPermissions.FileManagerDelete)]
    public async Task<ApiResult<int>> DeleteFiles([FromBody] List<long> batchIds)
    {
        try
        {
            var count = await _service.DeleteFilesAsync(batchIds);
            return ApiResult<int>.Success(count, $"成功删除 {count} 个文件");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除文件失败");
            return ApiResult<int>.Fail($"删除失败：{ex.Message}", 500);
        }
    }

    /// <summary>存储空间统计</summary>
    [HttpGet("stats")]
    [RequirePermission(CardFlowPermissions.FileManagerView)]
    public async Task<ApiResult<StorageStatsDto>> GetStorageStats()
    {
        try
        {
            var result = await _service.GetStorageStatsAsync();
            return ApiResult<StorageStatsDto>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取存储统计失败");
            return ApiResult<StorageStatsDto>.Fail($"查询失败：{ex.Message}", 500);
        }
    }

    /// <summary>清理策略列表</summary>
    [HttpGet("cleanup-policies")]
    [RequirePermission(CardFlowPermissions.FileManagerView)]
    public async Task<ApiResult<List<CleanupPolicyDto>>> GetCleanupPolicies()
    {
        var result = await _service.GetCleanupPoliciesAsync();
        return ApiResult<List<CleanupPolicyDto>>.Success(result);
    }

    /// <summary>创建/更新清理策略</summary>
    [HttpPost("cleanup-policies")]
    [RequirePermission(CardFlowPermissions.FileManagerDelete)]
    public async Task<ApiResult<CleanupPolicyDto>> SaveCleanupPolicy([FromBody] SaveCleanupPolicyRequest request)
    {
        try
        {
            var result = await _service.SaveCleanupPolicyAsync(request);
            return ApiResult<CleanupPolicyDto>.Success(result, request.Id.HasValue ? "策略更新成功" : "策略创建成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<CleanupPolicyDto>.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存清理策略失败");
            return ApiResult<CleanupPolicyDto>.Fail($"保存失败：{ex.Message}", 500);
        }
    }

    /// <summary>手动触发清理</summary>
    [HttpPost("cleanup")]
    [RequirePermission(CardFlowPermissions.FileManagerDelete)]
    public async Task<ApiResult<CleanupResultDto>> ExecuteCleanup()
    {
        try
        {
            var result = await _service.ExecuteCleanupAsync();
            return ApiResult<CleanupResultDto>.Success(result, $"清理完成，删除 {result.DeletedFileCount} 个文件，释放 {result.FreedSpace} 字节");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行清理失败");
            return ApiResult<CleanupResultDto>.Fail($"清理失败：{ex.Message}", 500);
        }
    }

    /// <summary>清理预览</summary>
    [HttpGet("cleanup-preview")]
    [RequirePermission(CardFlowPermissions.FileManagerView)]
    public async Task<ApiResult<CleanupPreviewDto>> PreviewCleanup()
    {
        try
        {
            var result = await _service.PreviewCleanupAsync();
            return ApiResult<CleanupPreviewDto>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清理预览失败");
            return ApiResult<CleanupPreviewDto>.Fail($"预览失败：{ex.Message}", 500);
        }
    }
}
