using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Task.Dtos;
using STOTOP.Module.Task.Services;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.Task.Controllers;

[Authorize]
[ApiController]
[Route("api/task/schedules")]
public class TaskScheduleController : ControllerBase
{
    private readonly ITaskScheduleService _service;

    public TaskScheduleController(ITaskScheduleService service)
    {
        _service = service;
    }

    /// <summary>调度列表</summary>
    [HttpGet]
    [RequirePermission(TaskPermissions.ScheduleManage)]
    public async global::System.Threading.Tasks.Task<ApiResult<PagedResult<TaskScheduleListDto>>> GetPagedList([FromQuery] SchedulePagedRequest query)
    {
        return await _service.GetPagedListAsync(query);
    }

    /// <summary>创建调度（定时/周期）</summary>
    [HttpPost]
    [RequirePermission(TaskPermissions.ScheduleManage)]
    public async global::System.Threading.Tasks.Task<ApiResult<TaskScheduleListDto>> Create([FromBody] CreateTaskScheduleRequest request)
    {
        return await _service.CreateAsync(request);
    }

    /// <summary>更新调度</summary>
    [HttpPut("{id}")]
    [RequirePermission(TaskPermissions.ScheduleManage)]
    public async global::System.Threading.Tasks.Task<ApiResult<TaskScheduleListDto>> Update(long id, [FromBody] UpdateTaskScheduleRequest request)
    {
        return await _service.UpdateAsync(id, request);
    }

    /// <summary>启用/禁用</summary>
    [HttpPut("{id}/toggle")]
    [RequirePermission(TaskPermissions.ScheduleManage)]
    public async global::System.Threading.Tasks.Task<ApiResult<bool>> Toggle(long id)
    {
        return await _service.ToggleAsync(id);
    }
}
