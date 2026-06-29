using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.CardFlow.Dtos;
using STOTOP.Module.CardFlow.Services.Download;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.CardFlow.Controllers;

[Authorize]
[ApiController]
[Route("api/cardflow/download-tasks")]
public class CfDownloadTaskController : ControllerBase
{
    private readonly IDownloadTaskService _taskService;
    private readonly DownloadJobService _jobService;

    public CfDownloadTaskController(IDownloadTaskService taskService, DownloadJobService jobService)
    {
        _taskService = taskService;
        _jobService = jobService;
    }

    /// <summary>
    /// 任务列表
    /// </summary>
    [HttpGet]
    [RequirePermission(CardFlowPermissions.Automation)]
    public async Task<ApiResult<List<DownloadTaskDto>>> GetList()
    {
        var result = await _taskService.GetListAsync();
        return ApiResult<List<DownloadTaskDto>>.Success(result);
    }

    /// <summary>
    /// 任务详情（含步骤）
    /// </summary>
    [HttpGet("{id:long}")]
    [RequirePermission(CardFlowPermissions.Automation)]
    public async Task<ApiResult<DownloadTaskDetailDto>> GetById(long id)
    {
        var result = await _taskService.GetByIdAsync(id);
        if (result == null)
            return ApiResult<DownloadTaskDetailDto>.Fail("下载任务不存在");
        return ApiResult<DownloadTaskDetailDto>.Success(result);
    }

    /// <summary>
    /// 创建任务
    /// </summary>
    [HttpPost]
    [RequirePermission(CardFlowPermissions.AutomationManage)]
    public async Task<ApiResult<DownloadTaskDetailDto>> Create([FromBody] DownloadTaskCreateDto dto)
    {
        try
        {
            var result = await _taskService.CreateAsync(dto);
            return ApiResult<DownloadTaskDetailDto>.Success(result, "创建成功");
        }
        catch (Exception ex)
        {
            return ApiResult<DownloadTaskDetailDto>.Fail($"创建失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 更新任务
    /// </summary>
    [HttpPut("{id:long}")]
    [RequirePermission(CardFlowPermissions.AutomationManage)]
    public async Task<ApiResult<DownloadTaskDetailDto>> Update(long id, [FromBody] DownloadTaskUpdateDto dto)
    {
        try
        {
            var result = await _taskService.UpdateAsync(id, dto);
            return ApiResult<DownloadTaskDetailDto>.Success(result, "更新成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<DownloadTaskDetailDto>.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            return ApiResult<DownloadTaskDetailDto>.Fail($"更新失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 删除任务
    /// </summary>
    [HttpDelete("{id:long}")]
    [RequirePermission(CardFlowPermissions.AutomationManage)]
    public async Task<ApiResult> Delete(long id)
    {
        try
        {
            await _taskService.DeleteAsync(id);
            return ApiResult.Ok("删除成功");
        }
        catch (Exception ex)
        {
            return ApiResult.Fail($"删除失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 立即执行一次
    /// </summary>
    [HttpPost("{id:long}/trigger")]
    [RequirePermission(CardFlowPermissions.AutomationManage)]
    public async Task<ApiResult> Trigger(long id)
    {
        try
        {
            await _jobService.TriggerOnceAsync(id);
            return ApiResult.Ok("已提交执行");
        }
        catch (Exception ex)
        {
            return ApiResult.Fail($"触发失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 指定任务的下载日志
    /// </summary>
    [HttpGet("{id:long}/logs")]
    [RequirePermission(CardFlowPermissions.Automation)]
    public async Task<ApiResult<List<DownloadLogDto>>> GetLogs(long id)
    {
        var result = await _taskService.GetLogsAsync(id);
        return ApiResult<List<DownloadLogDto>>.Success(result);
    }

    /// <summary>
    /// 全部下载日志
    /// </summary>
    [HttpGet("logs")]
    [RequirePermission(CardFlowPermissions.Automation)]
    public async Task<ApiResult<List<DownloadLogDto>>> GetAllLogs()
    {
        var result = await _taskService.GetLogsAsync(null);
        return ApiResult<List<DownloadLogDto>>.Success(result);
    }
}
