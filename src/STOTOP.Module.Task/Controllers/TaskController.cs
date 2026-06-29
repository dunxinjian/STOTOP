using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Task.Dtos;
using STOTOP.Module.Task.Services;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.Task.Controllers;

[Authorize]
[ApiController]
[Route("api/task/tasks")]
public class TaskController : ControllerBase
{
    private readonly ITaskService _service;

    public TaskController(ITaskService service)
    {
        _service = service;
    }

    private long GetUserId() => long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
    private long GetOrgId() => (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);
    private bool IsAdmin() => User.IsInRole("admin");

    /// <summary>任务列表（分页，支持多维筛选）</summary>
    [HttpGet]
    [RequirePermission(TaskPermissions.TaskView)]
    public async global::System.Threading.Tasks.Task<ApiResult<PagedResult<TaskListDto>>> GetPagedList([FromQuery] TaskPagedRequest query)
    {
        return await _service.GetPagedListAsync(query, GetOrgId(), GetUserId(), IsAdmin());
    }

    /// <summary>任务详情（含子任务、参与者）</summary>
    [HttpGet("{id}")]
    [RequirePermission(TaskPermissions.TaskView)]
    public async global::System.Threading.Tasks.Task<ApiResult<TaskDetailDto>> GetById(long id)
    {
        return await _service.GetByIdAsync(id);
    }

    /// <summary>创建任务</summary>
    [HttpPost]
    [RequirePermission(TaskPermissions.TaskCreate)]
    public async global::System.Threading.Tasks.Task<ApiResult<TaskDetailDto>> Create([FromBody] CreateTaskRequest request)
    {
        return await _service.CreateAsync(request, GetOrgId(), GetUserId());
    }

    /// <summary>更新任务</summary>
    [HttpPut("{id}")]
    [RequirePermission(TaskPermissions.TaskEdit)]
    public async global::System.Threading.Tasks.Task<ApiResult<TaskDetailDto>> Update(long id, [FromBody] UpdateTaskRequest request)
    {
        return await _service.UpdateAsync(id, request);
    }

    /// <summary>变更任务状态</summary>
    [HttpPut("{id}/status")]
    [RequirePermission(TaskPermissions.TaskEdit)]
    public async global::System.Threading.Tasks.Task<ApiResult<TaskDetailDto>> ChangeStatus(long id, [FromBody] ChangeTaskStatusRequest request)
    {
        return await _service.ChangeStatusAsync(id, request);
    }

    /// <summary>设置优先级</summary>
    [HttpPut("{id}/priority")]
    [RequirePermission(TaskPermissions.TaskEdit)]
    public async global::System.Threading.Tasks.Task<ApiResult<TaskDetailDto>> SetPriority(long id, [FromBody] int priority)
    {
        return await _service.SetPriorityAsync(id, priority);
    }

    /// <summary>分配执行人</summary>
    [HttpPut("{id}/assign")]
    [RequirePermission(TaskPermissions.TaskAssign)]
    public async global::System.Threading.Tasks.Task<ApiResult<TaskDetailDto>> Assign(long id, [FromBody] AssignTaskRequest request)
    {
        return await _service.AssignAsync(id, request);
    }

    /// <summary>创建子任务</summary>
    [HttpPost("{id}/subtasks")]
    [RequirePermission(TaskPermissions.TaskCreate)]
    public async global::System.Threading.Tasks.Task<ApiResult<TaskDetailDto>> CreateSubtask(long id, [FromBody] CreateTaskRequest request)
    {
        return await _service.CreateSubtaskAsync(id, request, GetOrgId(), GetUserId());
    }

    /// <summary>获取子任务列表</summary>
    [HttpGet("{id}/subtasks")]
    [RequirePermission(TaskPermissions.TaskView)]
    public async global::System.Threading.Tasks.Task<ApiResult<List<TaskListDto>>> GetSubtasks(long id)
    {
        return await _service.GetSubtasksAsync(id);
    }

    /// <summary>设置可见范围</summary>
    [HttpPut("{id}/visibility")]
    [RequirePermission(TaskPermissions.TaskEdit)]
    public async global::System.Threading.Tasks.Task<ApiResult<bool>> SetVisibility(long id, [FromBody] SetTaskVisibilityRequest request)
    {
        return await _service.SetVisibilityAsync(id, request);
    }

    /// <summary>获取任务依赖关系</summary>
    [HttpGet("{id}/dependencies")]
    [RequirePermission(TaskPermissions.TaskView)]
    public async global::System.Threading.Tasks.Task<ApiResult<List<TaskDependencyDto>>> GetDependencies(long id)
    {
        return await _service.GetDependenciesAsync(id);
    }

    /// <summary>添加任务依赖</summary>
    [HttpPost("{id}/dependencies")]
    [RequirePermission(TaskPermissions.TaskEdit)]
    public async global::System.Threading.Tasks.Task<ApiResult<TaskDependencyDto>> AddDependency(long id, [FromBody] AddTaskDependencyRequest request)
    {
        return await _service.AddDependencyAsync(id, request);
    }

    /// <summary>移除任务依赖</summary>
    [HttpDelete("{id}/dependencies/{depId}")]
    [RequirePermission(TaskPermissions.TaskEdit)]
    public async global::System.Threading.Tasks.Task<ApiResult<bool>> RemoveDependency(long id, long depId)
    {
        return await _service.RemoveDependencyAsync(id, depId);
    }

    /// <summary>获取任务标签</summary>
    [HttpGet("{id}/tags")]
    [RequirePermission(TaskPermissions.TaskView)]
    public async global::System.Threading.Tasks.Task<ApiResult<List<TagSimpleDto>>> GetTags(long id)
    {
        return await _service.GetTagsAsync(id);
    }

    /// <summary>设置任务标签</summary>
    [HttpPost("{id}/tags")]
    [RequirePermission(TaskPermissions.TaskEdit)]
    public async global::System.Threading.Tasks.Task<ApiResult<bool>> SetTags(long id, [FromBody] SetTaskTagsRequest request)
    {
        return await _service.SetTagsAsync(id, request);
    }

    /// <summary>我的待办任务数量（用于导航栏 badge）</summary>
    [HttpGet("~/api/task/my/count")]
    public async global::System.Threading.Tasks.Task<IActionResult> GetMyTaskCount()
    {
        var userId = GetUserId();
        var orgId = GetOrgId();
        var count = await _service.GetMyPendingCountAsync(orgId, userId);
        return Ok(count);
    }

    /// <summary>我的待办任务（待办/执行中）</summary>
    [HttpGet("my")]
    [RequirePermission(TaskPermissions.TaskView)]
    public async global::System.Threading.Tasks.Task<ApiResult<PagedResult<TaskListDto>>> GetMyTasks()
    {
        return await _service.GetMyTasksAsync(GetOrgId(), GetUserId());
    }

    /// <summary>删除任务</summary>
    [HttpDelete("{id}")]
    [RequirePermission(TaskPermissions.TaskDelete)]
    public async global::System.Threading.Tasks.Task<ApiResult<bool>> Delete(long id)
    {
        return await _service.DeleteAsync(id);
    }

    /// <summary>批量更新任务</summary>
    [HttpPut("batch")]
    [RequirePermission(TaskPermissions.TaskEdit)]
    public async global::System.Threading.Tasks.Task<ApiResult<bool>> BatchUpdate([FromBody] BatchUpdateRequest request)
    {
        return await _service.BatchUpdateAsync(request.TaskIds, request.Status, request.AssigneeId);
    }
}
