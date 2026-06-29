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
[Route("api/task/goals")]
public class GoalController : ControllerBase
{
    private readonly IGoalService _service;

    public GoalController(IGoalService service)
    {
        _service = service;
    }

    private long GetUserId() => long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
    private long GetOrgId() => (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);

    /// <summary>获取目标树（按组织层级）</summary>
    [HttpGet("tree")]
    [RequirePermission(TaskPermissions.GoalView)]
    public async global::System.Threading.Tasks.Task<ApiResult<List<GoalTreeDto>>> GetTree([FromQuery] GoalTreeQueryRequest query)
    {
        return await _service.GetTreeAsync(query, GetOrgId());
    }

    /// <summary>获取目标详情（含KR列表）</summary>
    [HttpGet("{id}")]
    [RequirePermission(TaskPermissions.GoalView)]
    public async global::System.Threading.Tasks.Task<ApiResult<GoalDetailDto>> GetById(long id)
    {
        return await _service.GetByIdAsync(id);
    }

    private static readonly HashSet<string> ValidLevels = new() { "yearly", "quarterly", "monthly" };

    /// <summary>创建目标</summary>
    [HttpPost]
    [RequirePermission(TaskPermissions.GoalCreate)]
    public async global::System.Threading.Tasks.Task<ApiResult<GoalDetailDto>> Create([FromBody] CreateGoalRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return ApiResult<GoalDetailDto>.Fail("目标标题不能为空");
        if (request.GoalOrgId <= 0)
            return ApiResult<GoalDetailDto>.Fail("目标组织不能为空");
        if (!ValidLevels.Contains(request.Level))
            return ApiResult<GoalDetailDto>.Fail("目标周期不在有效范围内");
        if (request.StartDate >= request.EndDate)
            return ApiResult<GoalDetailDto>.Fail("开始日期必须早于截止日期");

        try
        {
            return await _service.CreateAsync(request, GetOrgId(), GetUserId());
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<GoalDetailDto>.Fail(ex.Message);
        }
    }

    /// <summary>更新目标</summary>
    [HttpPut("{id}")]
    [RequirePermission(TaskPermissions.GoalEdit)]
    public async global::System.Threading.Tasks.Task<ApiResult<GoalDetailDto>> Update(long id, [FromBody] UpdateGoalRequest request)
    {
        return await _service.UpdateAsync(id, request);
    }

    /// <summary>分解目标到下级</summary>
    [HttpPost("{id}/decompose")]
    [RequirePermission(TaskPermissions.GoalDecompose)]
    public async global::System.Threading.Tasks.Task<ApiResult<GoalDetailDto>> Decompose(long id, [FromBody] DecomposeGoalRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return ApiResult<GoalDetailDto>.Fail("子目标标题不能为空");
        if (request.GoalOrgId <= 0)
            return ApiResult<GoalDetailDto>.Fail("目标组织不能为空");
        if (!ValidLevels.Contains(request.Level))
            return ApiResult<GoalDetailDto>.Fail("目标周期不在有效范围内");
        if (request.StartDate >= request.EndDate)
            return ApiResult<GoalDetailDto>.Fail("开始日期必须早于截止日期");

        try
        {
            return await _service.DecomposeAsync(id, request, GetOrgId(), GetUserId());
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<GoalDetailDto>.Fail(ex.Message);
        }
    }

    /// <summary>获取子目标</summary>
    [HttpGet("{id}/children")]
    [RequirePermission(TaskPermissions.GoalView)]
    public async global::System.Threading.Tasks.Task<ApiResult<List<GoalListDto>>> GetChildren(long id)
    {
        return await _service.GetChildrenAsync(id);
    }

    /// <summary>获取目标关联的任务</summary>
    [HttpGet("{id}/tasks")]
    [RequirePermission(TaskPermissions.GoalView)]
    public async global::System.Threading.Tasks.Task<ApiResult<List<TaskListDto>>> GetTasks(long id)
    {
        return await _service.GetTasksAsync(id);
    }
}
