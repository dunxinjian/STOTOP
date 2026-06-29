namespace STOTOP.Module.Conference.Dtos;

/// <summary>
/// 桌次详情DTO
/// </summary>
public class TableDto
{
    public long Id { get; set; }
    public long MealPlanId { get; set; }
    public int TableNumber { get; set; }
    public string? TableName { get; set; }
    public int SeatCount { get; set; }
    public string? Remark { get; set; }
    public DateTime UpdatedTime { get; set; }
    /// <summary>座位安排</summary>
    public List<TableSeatDto> Seats { get; set; } = new();
}

/// <summary>
/// 桌次列表项DTO
/// </summary>
public class TableListItemDto
{
    public long Id { get; set; }
    public long MealPlanId { get; set; }
    public int TableNumber { get; set; }
    public string? TableName { get; set; }
    public int SeatCount { get; set; }
    /// <summary>已安排座位数</summary>
    public int OccupiedSeats { get; set; }
}

/// <summary>
/// 桌次座位DTO
/// </summary>
public class TableSeatDto
{
    public long Id { get; set; }
    public long TableId { get; set; }
    public long AttendeeId { get; set; }
    public string? AttendeeName { get; set; }
    public string? Organization { get; set; }
    public string? Role { get; set; }
    public int SeatNumber { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 创建桌次请求
/// </summary>
public class CreateTableRequest
{
    public int TableNumber { get; set; }
    public string? TableName { get; set; }
    public int SeatCount { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 更新桌次请求
/// </summary>
public class UpdateTableRequest
{
    public int TableNumber { get; set; }
    public string? TableName { get; set; }
    public int SeatCount { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 设置桌次座位请求
/// </summary>
public class TableSeatRequest
{
    /// <summary>座位安排列表</summary>
    public List<SeatInput> Seats { get; set; } = new();
}

/// <summary>
/// 座位输入项
/// </summary>
public class SeatInput
{
    public long AttendeeId { get; set; }
    public int SeatNumber { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 智能编桌配置请求
/// </summary>
public class AutoArrangeConfigRequest
{
    /// <summary>每桌座位数（默认10）</summary>
    public int SeatsPerTable { get; set; } = 10;
    /// <summary>是否按单位聚合</summary>
    public bool GroupByOrganization { get; set; } = true;
    /// <summary>是否按饮食偏好聚合</summary>
    public bool GroupByDiet { get; set; } = true;
    /// <summary>是否设置主桌（自动选取嘉宾/领导）</summary>
    public bool EnableMainTable { get; set; } = true;
}

/// <summary>
/// 智能编桌预览DTO
/// </summary>
public class AutoArrangePreviewDto
{
    /// <summary>桌次安排方案</summary>
    public List<TableArrangePreviewItem> Tables { get; set; } = new();
    /// <summary>未安排的人员</summary>
    public List<string> UnseatedAttendees { get; set; } = new();
    /// <summary>总桌数</summary>
    public int TotalTables { get; set; }
    /// <summary>总人数</summary>
    public int TotalPersons { get; set; }
}

/// <summary>
/// 桌次安排预览子项
/// </summary>
public class TableArrangePreviewItem
{
    public int TableNumber { get; set; }
    public string? TableName { get; set; }
    public int SeatCount { get; set; }
    public List<AssignedGuestItem> Guests { get; set; } = new();
}
