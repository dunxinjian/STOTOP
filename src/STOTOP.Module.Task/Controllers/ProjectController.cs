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
[Route("api/task/projects")]
public class ProjectController : ControllerBase
{
    private readonly IProjectService _projectService;
    private readonly ITaskService _taskService;
    private readonly IKanbanService _kanbanService;

    public ProjectController(IProjectService projectService, ITaskService taskService, IKanbanService kanbanService)
    {
        _projectService = projectService;
        _taskService = taskService;
        _kanbanService = kanbanService;
    }

    private long GetUserId() => long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
    private long GetOrgId() => (long)(HttpContext.Items["CurrentOrgId"] ?? 0L);
    private bool IsAdmin() => User.IsInRole("admin");

    /// <summary>项目列表（分页）</summary>
    [HttpGet]
    [RequirePermission(TaskPermissions.ProjectView)]
    public async global::System.Threading.Tasks.Task<ApiResult<PagedResult<ProjectListDto>>> GetPagedList([FromQuery] ProjectPagedRequest query)
    {
        return await _projectService.GetPagedListAsync(query, GetOrgId());
    }

    /// <summary>项目详情</summary>
    [HttpGet("{id}")]
    [RequirePermission(TaskPermissions.ProjectView)]
    public async global::System.Threading.Tasks.Task<ApiResult<ProjectDetailDto>> GetById(long id)
    {
        return await _projectService.GetByIdAsync(id);
    }

    /// <summary>创建项目</summary>
    [HttpPost]
    [RequirePermission(TaskPermissions.ProjectCreate)]
    public async global::System.Threading.Tasks.Task<ApiResult<ProjectDetailDto>> Create([FromBody] CreateProjectRequest request)
    {
        return await _projectService.CreateAsync(request, GetOrgId(), GetUserId());
    }

    /// <summary>更新项目</summary>
    [HttpPut("{id}")]
    [RequirePermission(TaskPermissions.ProjectEdit)]
    public async global::System.Threading.Tasks.Task<ApiResult<ProjectDetailDto>> Update(long id, [FromBody] UpdateProjectRequest request)
    {
        return await _projectService.UpdateAsync(id, request);
    }

    /// <summary>项目下的任务列表</summary>
    [HttpGet("{id}/tasks")]
    [RequirePermission(TaskPermissions.ProjectView)]
    public async global::System.Threading.Tasks.Task<ApiResult<PagedResult<TaskListDto>>> GetTasks(long id, [FromQuery] TaskPagedRequest query)
    {
        query.ProjectId = id;
        return await _taskService.GetPagedListAsync(query, GetOrgId(), GetUserId(), IsAdmin());
    }

    /// <summary>项目看板视图</summary>
    [HttpGet("{id}/kanban")]
    [RequirePermission(TaskPermissions.ProjectView)]
    public async global::System.Threading.Tasks.Task<ApiResult<KanbanDataDto>> GetKanban(long id, [FromQuery] KanbanQueryRequest query)
    {
        query.ProjectId = id;
        return await _kanbanService.GetKanbanDataAsync(query, GetOrgId(), GetUserId(), IsAdmin());
    }

    /// <summary>获取项目成员列表</summary>
    [HttpGet("{id}/members")]
    [RequirePermission(TaskPermissions.ProjectMember)]
    public async global::System.Threading.Tasks.Task<ApiResult<List<ProjectMemberDto>>> GetMembers(long id)
    {
        return await _projectService.GetMembersAsync(id);
    }

    /// <summary>添加项目成员</summary>
    [HttpPost("{id}/members")]
    [RequirePermission(TaskPermissions.ProjectMember)]
    public async global::System.Threading.Tasks.Task<ApiResult<ProjectMemberDto>> AddMember(long id, [FromBody] AddProjectMemberRequest request)
    {
        return await _projectService.AddMemberAsync(id, request);
    }

    /// <summary>移除项目成员</summary>
    [HttpDelete("{id}/members/{userId}")]
    [RequirePermission(TaskPermissions.ProjectMember)]
    public async global::System.Threading.Tasks.Task<ApiResult<bool>> RemoveMember(long id, long userId)
    {
        return await _projectService.RemoveMemberAsync(id, userId);
    }
}
