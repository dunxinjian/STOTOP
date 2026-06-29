using STOTOP.Core.Models;

namespace STOTOP.Module.Task.Dtos;

/// <summary>
/// 任务提醒列表DTO
/// </summary>
public class TaskReminderListDto
{
    public long Id { get; set; }
    public long TaskId { get; set; }
    public string? TaskTitle { get; set; }
    public long UserId { get; set; }
    public DateTime ReminderTime { get; set; }
    public int ReminderType { get; set; }
    public bool IsRead { get; set; }
    public bool IsSent { get; set; }
    public DateTime CreateTime { get; set; }
}

/// <summary>
/// 创建任务提醒请求
/// </summary>
public class CreateTaskReminderRequest
{
    public long TaskId { get; set; }
    public long UserId { get; set; }
    public DateTime ReminderTime { get; set; }
    public int ReminderType { get; set; }
}

/// <summary>
/// 提醒分页查询请求
/// </summary>
public class ReminderPagedRequest : PagedRequest
{
    public int? ReminderType { get; set; }
    public bool? IsRead { get; set; }
    public bool? IsSent { get; set; }
}
