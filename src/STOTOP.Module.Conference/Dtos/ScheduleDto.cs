namespace STOTOP.Module.Conference.Dtos;

/// <summary>
/// 日程详情DTO
/// </summary>
public class ScheduleDto
{
    public long Id { get; set; }
    public long EventId { get; set; }
    public DateTime Date { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? Type { get; set; }
    public string? Description { get; set; }
    public int Sort { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
    /// <summary>参会人列表</summary>
    public List<AttendeeListItemDto> Attendees { get; set; } = new();
    /// <summary>所需物品列表</summary>
    public List<ScheduleItemDto> Items { get; set; } = new();
}

/// <summary>
/// 日程列表项DTO
/// </summary>
public class ScheduleListItemDto
{
    public long Id { get; set; }
    public long EventId { get; set; }
    public DateTime Date { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? Type { get; set; }
    public int Sort { get; set; }
    /// <summary>参会人数</summary>
    public int AttendeeCount { get; set; }
    /// <summary>物品数</summary>
    public int ItemCount { get; set; }
}

/// <summary>
/// 日程物品DTO
/// </summary>
public class ScheduleItemDto
{
    public long Id { get; set; }
    public long ScheduleId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string? Unit { get; set; }
    public string? Status { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 创建日程请求
/// </summary>
public class CreateScheduleRequest
{
    public DateTime Date { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? Type { get; set; }
    public string? Description { get; set; }
    public int Sort { get; set; }
}

/// <summary>
/// 更新日程请求
/// </summary>
public class UpdateScheduleRequest
{
    public DateTime Date { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? Type { get; set; }
    public string? Description { get; set; }
    public int Sort { get; set; }
}

/// <summary>
/// 设置日程参会人请求
/// </summary>
public class ScheduleAttendeeRequest
{
    /// <summary>参会人ID列表</summary>
    public List<long> AttendeeIds { get; set; } = new();
}

/// <summary>
/// 设置日程物品请求
/// </summary>
public class ScheduleItemRequest
{
    /// <summary>物品列表</summary>
    public List<ScheduleItemInput> Items { get; set; } = new();
}

/// <summary>
/// 日程物品输入项
/// </summary>
public class ScheduleItemInput
{
    public string ItemName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string? Unit { get; set; }
    public string? Remark { get; set; }
}
