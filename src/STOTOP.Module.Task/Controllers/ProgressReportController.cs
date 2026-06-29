using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Task.Dtos;
using STOTOP.Module.Task.Services;
using STOTOP.Module.Task.Services.DingTalk;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.Task.Controllers;

[Authorize]
[ApiController]
[Route("api/task/tasks/{taskId}/progress")]
public class ProgressReportController : ControllerBase
{
    private readonly IProgressReportService _service;
    private readonly IDingTalkMessageService _dingTalkService;

    public ProgressReportController(IProgressReportService service, IDingTalkMessageService dingTalkService)
    {
        _service = service;
        _dingTalkService = dingTalkService;
    }

    private long GetUserId() => long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

    /// <summary>提交进度上报（进度值+说明+附件+工时）</summary>
    [HttpPost]
    [RequirePermission(TaskPermissions.TaskEdit)]
    public async global::System.Threading.Tasks.Task<ApiResult<ProgressReportListDto>> Create(long taskId, [FromBody] CreateProgressReportRequest request)
    {
        return await _service.CreateAsync(taskId, request, GetUserId());
    }

    /// <summary>获取进度上报历史列表</summary>
    [HttpGet]
    [RequirePermission(TaskPermissions.TaskView)]
    public async global::System.Threading.Tasks.Task<ApiResult<PagedResult<ProgressReportListDto>>> GetPagedList(long taskId, [FromQuery] ProgressReportPagedRequest query)
    {
        return await _service.GetPagedListAsync(taskId, query);
    }

    /// <summary>选择发送进度上报到钉钉</summary>
    [HttpPost("{pid}/push-dingtalk")]
    [RequirePermission(TaskPermissions.TaskEdit)]
    public async global::System.Threading.Tasks.Task<ApiResult<bool>> PushToDingTalk(long taskId, long pid)
    {
        await _dingTalkService.PushProgressAsync(pid);
        return ApiResult<bool>.Success(true);
    }
}
