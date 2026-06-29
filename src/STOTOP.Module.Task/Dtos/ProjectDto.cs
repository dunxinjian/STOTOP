using STOTOP.Core.Models;

namespace STOTOP.Module.Task.Dtos;

/// <summary>
/// 项目列表DTO
/// </summary>
public class ProjectListDto
{
    public long Id { get; set; }
    public string UID { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public long OrgId { get; set; }
    public long? GoalId { get; set; }
    public string? GoalTitle { get; set; }
    public long ManagerId { get; set; }
    public string? ManagerName { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int Status { get; set; }
    public int MemberCount { get; set; }
    public int TaskCount { get; set; }
    public int CompletedTaskCount { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
}

/// <summary>
/// 项目详情DTO（含成员列表+任务统计）
/// </summary>
public class ProjectDetailDto
{
    public long Id { get; set; }
    public string UID { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public long OrgId { get; set; }
    public long? GoalId { get; set; }
    public string? GoalTitle { get; set; }
    public long ManagerId { get; set; }
    public string? ManagerName { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int Status { get; set; }
    public long CreatorId { get; set; }
    public string? CreatorName { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
    public int TaskCount { get; set; }
    public int CompletedTaskCount { get; set; }
    public int InProgressTaskCount { get; set; }
    public int OverdueTaskCount { get; set; }
    public List<ProjectMemberDto> Members { get; set; } = new();
}

/// <summary>
/// 项目成员DTO
/// </summary>
public class ProjectMemberDto
{
    public long Id { get; set; }
    public long ProjectId { get; set; }
    public long UserId { get; set; }
    public string? UserName { get; set; }
    public int Role { get; set; }
    public DateTime JoinTime { get; set; }
}

/// <summary>
/// 创建项目请求
/// </summary>
public class CreateProjectRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public long? GoalId { get; set; }
    public long ManagerId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

/// <summary>
/// 更新项目请求
/// </summary>
public class UpdateProjectRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public long? GoalId { get; set; }
    public long ManagerId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int Status { get; set; }
}

/// <summary>
/// 添加项目成员请求
/// </summary>
public class AddProjectMemberRequest
{
    public long UserId { get; set; }
    public int Role { get; set; } = 1;
}

/// <summary>
/// 项目分页查询请求
/// </summary>
public class ProjectPagedRequest : PagedRequest
{
    public int? Status { get; set; }
    public long? ManagerId { get; set; }
}
