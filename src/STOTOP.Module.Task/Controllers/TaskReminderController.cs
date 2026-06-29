using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Task.Dtos;
using STOTOP.Module.Task.Services;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.Task.Controllers;

[Authorize]
[ApiController]
[Route("api/task/reminders")]
public class TaskReminderController : ControllerBase
{
    private readonly ITaskReminderService _service;

    public TaskReminderController(ITaskReminderService service)
    {
        _service = service;
    }

    /// <summary>获取任务提醒列表</summary>
    [HttpGet("{taskId}")]
    [RequirePermission(TaskPermissions.ReminderManage)]
    public async global::System.Threading.Tasks.Task<ApiResult<List<TaskReminderListDto>>> GetList(long taskId)
    {
        return await _service.GetListAsync(taskId);
    }

    /// <summary>创建提醒</summary>
    [HttpPost("{taskId}")]
    [RequirePermission(TaskPermissions.ReminderManage)]
    public async global::System.Threading.Tasks.Task<ApiResult<TaskReminderListDto>> Create(long taskId, [FromBody] CreateTaskReminderRequest request)
    {
        return await _service.CreateAsync(taskId, request);
    }

    /// <summary>删除提醒</summary>
    [HttpDelete("{id}")]
    [RequirePermission(TaskPermissions.ReminderManage)]
    public async global::System.Threading.Tasks.Task<ApiResult<bool>> Delete(long id)
    {
        return await _service.DeleteAsync(id);
    }
}
