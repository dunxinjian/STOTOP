namespace STOTOP.Module.Conference.Dtos;

/// <summary>
/// 活动详情DTO
/// </summary>
public class EventDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? Location { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? GroomName { get; set; }
    public string? BrideName { get; set; }
    public string? Manager { get; set; }
    public string? ManagerPhone { get; set; }
    public decimal Budget { get; set; }
    public string? Remark { get; set; }
    public string? Creator { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
}

/// <summary>
/// 活动列表项DTO
/// </summary>
public class EventListItemDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? Location { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Manager { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? GroomName { get; set; }
    public string? BrideName { get; set; }
    public decimal Budget { get; set; }
    public DateTime CreatedTime { get; set; }
    /// <summary>参会人数</summary>
    public int AttendeeCount { get; set; }
}

/// <summary>
/// 创建活动请求
/// </summary>
public class CreateEventRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? Location { get; set; }
    public string? Manager { get; set; }
    public string? ManagerPhone { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? GroomName { get; set; }
    public string? BrideName { get; set; }
    public decimal Budget { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 更新活动请求
/// </summary>
public class UpdateEventRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? Location { get; set; }
    public string? Status { get; set; }
    public string? Manager { get; set; }
    public string? ManagerPhone { get; set; }
    public string? Type { get; set; }
    public string? GroomName { get; set; }
    public string? BrideName { get; set; }
    public decimal Budget { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 活动查询请求
/// </summary>
public class EventQueryRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Keyword { get; set; }
    public string? Status { get; set; }
}

/// <summary>
/// 活动仪表板统计DTO
/// </summary>
public class DashboardDto
{
    /// <summary>参会人总数</summary>
    public int TotalAttendees { get; set; }
    /// <summary>已确认人数</summary>
    public int ConfirmedAttendees { get; set; }
    /// <summary>日程项总数</summary>
    public int TotalSchedules { get; set; }
    /// <summary>车辆总数</summary>
    public int TotalVehicles { get; set; }
    /// <summary>接送任务总数</summary>
    public int TotalPickupTasks { get; set; }
    /// <summary>待安排接送</summary>
    public int PendingPickupTasks { get; set; }
    /// <summary>已使用车辆数（已分配接送任务的不同车辆）</summary>
    public int UsedVehicles { get; set; }
    /// <summary>已安排接送的乘客数</summary>
    public int ArrangedPassengers { get; set; }
    /// <summary>待接送总乘客数</summary>
    public int TotalPassengers { get; set; }
    /// <summary>酒店数</summary>
    public int TotalHotels { get; set; }
    /// <summary>房间总数</summary>
    public int TotalRooms { get; set; }
    /// <summary>已分配房间数</summary>
    public int AssignedRooms { get; set; }
    /// <summary>餐次总数</summary>
    public int TotalMealPlans { get; set; }
    /// <summary>物品总数</summary>
    public int TotalMaterials { get; set; }
    /// <summary>物品到位数</summary>
    public int ReceivedMaterials { get; set; }
    /// <summary>收入总额</summary>
    public decimal TotalIncome { get; set; }
    /// <summary>预算金额</summary>
    public decimal Budget { get; set; }
    /// <summary>异常告警数</summary>
    public int AlertCount { get; set; }
}

/// <summary>
/// 异常告警项DTO
/// </summary>
public class AlertItemDto
{
    /// <summary>告警级别：Warning/Error</summary>
    public string Level { get; set; } = string.Empty;
    /// <summary>告警类别：Transport/Accommodation/Meal/Material/Schedule</summary>
    public string Category { get; set; } = string.Empty;
    /// <summary>告警标题</summary>
    public string Title { get; set; } = string.Empty;
    /// <summary>告警详情</summary>
    public string? Detail { get; set; }
    /// <summary>关联实体ID</summary>
    public long? RelatedEntityId { get; set; }
    /// <summary>关联实体类型</summary>
    public string? RelatedEntityType { get; set; }
}
