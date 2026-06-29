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
[Route("api/task/tasks/{taskId}/comments")]
public class TaskCommentController : ControllerBase
{
    private readonly ITaskCommentService _service;
    private readonly IDingTalkMessageService _dingTalkService;

    public TaskCommentController(ITaskCommentService service, IDingTalkMessageService dingTalkService)
    {
        _service = service;
        _dingTalkService = dingTalkService;
    }

    private long GetUserId() => long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

    /// <summary>获取评论列表（含表情统计、附件）</summary>
    [HttpGet]
    [RequirePermission(TaskPermissions.TaskView)]
    public async global::System.Threading.Tasks.Task<ApiResult<PagedResult<TaskCommentListDto>>> GetPagedList(long taskId, [FromQuery] CommentPagedRequest query)
    {
        return await _service.GetPagedListAsync(taskId, query);
    }

    /// <summary>添加评论（支持富文本、@提及）</summary>
    [HttpPost]
    [RequirePermission(TaskPermissions.TaskEdit)]
    public async global::System.Threading.Tasks.Task<ApiResult<TaskCommentListDto>> Create(long taskId, [FromBody] CreateTaskCommentRequest request)
    {
        return await _service.CreateAsync(taskId, request, GetUserId());
    }

    /// <summary>编辑评论</summary>
    [HttpPut("{cid}")]
    [RequirePermission(TaskPermissions.TaskEdit)]
    public async global::System.Threading.Tasks.Task<ApiResult<TaskCommentListDto>> Update(long taskId, long cid, [FromBody] UpdateTaskCommentRequest request)
    {
        return await _service.UpdateAsync(taskId, cid, request);
    }

    /// <summary>删除评论</summary>
    [HttpDelete("{cid}")]
    [RequirePermission(TaskPermissions.TaskEdit)]
    public async global::System.Threading.Tasks.Task<ApiResult<bool>> Delete(long taskId, long cid)
    {
        return await _service.DeleteAsync(taskId, cid);
    }

    /// <summary>添加/切换表情回应</summary>
    [HttpPost("{cid}/reactions")]
    [RequirePermission(TaskPermissions.TaskView)]
    public async global::System.Threading.Tasks.Task<ApiResult<List<ReactionSummaryDto>>> ToggleReaction(long taskId, long cid, [FromBody] ToggleReactionRequest request)
    {
        return await _service.ToggleReactionAsync(taskId, cid, request, GetUserId());
    }

    /// <summary>移除表情</summary>
    [HttpDelete("{cid}/reactions/{emoji}")]
    [RequirePermission(TaskPermissions.TaskView)]
    public async global::System.Threading.Tasks.Task<ApiResult<bool>> RemoveReaction(long taskId, long cid, string emoji)
    {
        return await _service.RemoveReactionAsync(taskId, cid, emoji, GetUserId());
    }

    /// <summary>选择发送评论到钉钉</summary>
    [HttpPost("{cid}/push-dingtalk")]
    [RequirePermission(TaskPermissions.TaskEdit)]
    public async global::System.Threading.Tasks.Task<ApiResult<bool>> PushToDingTalk(long taskId, long cid)
    {
        await _dingTalkService.PushCommentAsync(cid);
        return ApiResult<bool>.Success(true);
    }
}
