using STOTOP.Core.Models;

namespace STOTOP.Module.Task.Dtos;

/// <summary>
/// 任务调度列表DTO
/// </summary>
public class TaskScheduleListDto
{
    public long Id { get; set; }
    public long TemplateTaskId { get; set; }
    public string? TemplateTaskTitle { get; set; }
    public int ScheduleType { get; set; }
    public string? CronExpression { get; set; }
    public DateTime? ScheduledTime { get; set; }
    public DateTime? NextExecution { get; set; }
    public DateTime? LastExecution { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime CreateTime { get; set; }
}

/// <summary>
/// 创建任务调度请求（含Cron表达式+定时时间+模板任务ID）
/// </summary>
public class CreateTaskScheduleRequest
{
    public long TemplateTaskId { get; set; }
    public int ScheduleType { get; set; }
    public string? CronExpression { get; set; }
    public DateTime? ScheduledTime { get; set; }
}

/// <summary>
/// 更新任务调度请求
/// </summary>
public class UpdateTaskScheduleRequest
{
    public int ScheduleType { get; set; }
    public string? CronExpression { get; set; }
    public DateTime? ScheduledTime { get; set; }
    public bool IsEnabled { get; set; }
}

/// <summary>
/// 启用/禁用调度请求
/// </summary>
public class ToggleScheduleRequest
{
    public bool IsEnabled { get; set; }
}

/// <summary>
/// 调度分页查询请求
/// </summary>
public class SchedulePagedRequest : PagedRequest
{
    public int? ScheduleType { get; set; }
    public bool? IsEnabled { get; set; }
}
