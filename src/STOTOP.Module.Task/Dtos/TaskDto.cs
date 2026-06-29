using STOTOP.Core.Models;

namespace STOTOP.Module.Task.Dtos;

/// <summary>
/// 任务列表DTO
/// </summary>
public class TaskListDto
{
    public long Id { get; set; }
    public string UID { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public long OrgId { get; set; }
    public long? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public long? GoalId { get; set; }
    public long? KRId { get; set; }
    public long ParentTaskId { get; set; }
    public int Type { get; set; }
    public int Priority { get; set; }
    public int Status { get; set; }
    public long? AssigneeId { get; set; }
    public string? AssigneeName { get; set; }
    public long CreatorId { get; set; }
    public string? CreatorName { get; set; }
    public DateTime? PlanStart { get; set; }
    public DateTime? PlanEnd { get; set; }
    public int Progress { get; set; }
    public string? Code { get; set; }
    public int SubTaskCount { get; set; }
    public int CompletedSubTaskCount { get; set; }
    public List<TagSimpleDto> Tags { get; set; } = new();
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
}

/// <summary>
/// 任务详情DTO（含子任务+参与者+标签+依赖）
/// </summary>
public class TaskDetailDto
{
    public long Id { get; set; }
    public string UID { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public long OrgId { get; set; }
    public long? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public long? GoalId { get; set; }
    public string? GoalTitle { get; set; }
    public long? KRId { get; set; }
    public string? KRTitle { get; set; }
    public long ParentTaskId { get; set; }
    public int Type { get; set; }
    public int Priority { get; set; }
    public int Status { get; set; }
    public long? AssigneeId { get; set; }
    public string? AssigneeName { get; set; }
    public long CreatorId { get; set; }
    public string? CreatorName { get; set; }
    public DateTime? PlanStart { get; set; }
    public DateTime? PlanEnd { get; set; }
    public DateTime? ActualStart { get; set; }
    public DateTime? ActualEnd { get; set; }
    public decimal? EstimatedHours { get; set; }
    public decimal? ActualHours { get; set; }
    public int Progress { get; set; }
    public int Visibility { get; set; }
    public bool IsTemplate { get; set; }
    public string? Code { get; set; }
    public int Sort { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
    public List<TaskListDto> SubTasks { get; set; } = new();
    public List<TaskMemberDto> Members { get; set; } = new();
    public List<TagSimpleDto> Tags { get; set; } = new();
    public List<TaskDependencyDto> Dependencies { get; set; } = new();
}

/// <summary>
/// 任务参与者DTO
/// </summary>
public class TaskMemberDto
{
    public long Id { get; set; }
    public long TaskId { get; set; }
    public long UserId { get; set; }
    public string? UserName { get; set; }
    public int Role { get; set; }
}

/// <summary>
/// 任务依赖DTO
/// </summary>
public class TaskDependencyDto
{
    public long Id { get; set; }
    public long TaskId { get; set; }
    public long DependsOnTaskId { get; set; }
    public string? DependsOnTaskTitle { get; set; }
    public int DependsOnTaskStatus { get; set; }
    public int DependencyType { get; set; }
}

/// <summary>
/// 标签简要DTO（嵌入任务列表使用）
/// </summary>
public class TagSimpleDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
}

/// <summary>
/// 创建任务请求
/// </summary>
public class CreateTaskRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public long? ProjectId { get; set; }
    public long? GoalId { get; set; }
    public long? KRId { get; set; }
    public long ParentTaskId { get; set; } = 0;
    public int Type { get; set; } = 0;
    public int Priority { get; set; } = 2;
    public long? AssigneeId { get; set; }
    public DateTime? PlanStart { get; set; }
    public DateTime? PlanEnd { get; set; }
    public decimal? EstimatedHours { get; set; }
    public int Visibility { get; set; } = 0;
    public List<long>? TagIds { get; set; }
    public List<long>? MemberUserIds { get; set; }
}

/// <summary>
/// 更新任务请求
/// </summary>
public class UpdateTaskRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public long? ProjectId { get; set; }
    public long? GoalId { get; set; }
    public long? KRId { get; set; }
    public int Type { get; set; }
    public int Priority { get; set; }
    public long? AssigneeId { get; set; }
    public DateTime? PlanStart { get; set; }
    public DateTime? PlanEnd { get; set; }
    public decimal? EstimatedHours { get; set; }
    public int Visibility { get; set; }
    public List<long>? TagIds { get; set; }
}

/// <summary>
/// 任务状态变更请求
/// </summary>
public class ChangeTaskStatusRequest
{
    public int Status { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 任务分配请求
/// </summary>
public class AssignTaskRequest
{
    public long? AssigneeId { get; set; }
}

/// <summary>
/// 设置任务标签请求
/// </summary>
public class SetTaskTagsRequest
{
    public List<long> TagIds { get; set; } = new();
}

/// <summary>
/// 添加任务依赖请求
/// </summary>
public class AddTaskDependencyRequest
{
    public long DependsOnTaskId { get; set; }
    public int DependencyType { get; set; } = 0;
}

/// <summary>
/// 设置任务可见范围请求
/// </summary>
public class SetTaskVisibilityRequest
{
    public int Visibility { get; set; }
    public List<TaskVisibilityRuleDto>? Rules { get; set; }
}

/// <summary>
/// 任务可见范围规则DTO
/// </summary>
public class TaskVisibilityRuleDto
{
    public int TargetType { get; set; }
    public long TargetId { get; set; }
}

/// <summary>
/// 任务多维筛选查询请求
/// </summary>
public class TaskPagedRequest : PagedRequest
{
    public int? Status { get; set; }
    public int? Priority { get; set; }
    public long? AssigneeId { get; set; }
    public long? ProjectId { get; set; }
    public long? GoalId { get; set; }
    public long? KRId { get; set; }
    public int? Type { get; set; }
    public long? ParentTaskId { get; set; }
    public List<long>? TagIds { get; set; }
    public DateTime? PlanStartFrom { get; set; }
    public DateTime? PlanStartTo { get; set; }
    public DateTime? PlanEndFrom { get; set; }
    public DateTime? PlanEndTo { get; set; }
    public bool? IsTemplate { get; set; }
}

/// <summary>
/// 批量更新任务请求
/// </summary>
public class BatchUpdateRequest
{
    public List<long> TaskIds { get; set; } = new();
    public int? Status { get; set; }
    public long? AssigneeId { get; set; }
}
