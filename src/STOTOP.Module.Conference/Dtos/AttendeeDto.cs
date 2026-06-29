namespace STOTOP.Module.Conference.Dtos;

/// <summary>
/// 参会人员详情DTO（含全景关联信息）
/// </summary>
public class AttendeeDto
{
    public long Id { get; set; }
    public long EventId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Gender { get; set; }
    public string? Phone { get; set; }
    public string? Organization { get; set; }
    public string? Title { get; set; }
    public string? Role { get; set; }
    public string? DietPreference { get; set; }
    public string? ArrivalMode { get; set; }
    public string? ArrivalFlightTrain { get; set; }
    public DateTime? ArrivalTime { get; set; }
    public string? ArrivalStation { get; set; }
    public string? DepartureMode { get; set; }
    public string? DepartureFlightTrain { get; set; }
    public DateTime? DepartureTime { get; set; }
    public string? DepartureStation { get; set; }
    public bool NeedPickup { get; set; }
    public bool NeedAccommodation { get; set; }
    public string? PreferredRoomType { get; set; }
    public long? PrimaryGuestId { get; set; }
    public string? Relation { get; set; }
    public bool IsChild { get; set; }
    public int? Age { get; set; }
    public string? Camp { get; set; }
    public string? GuestType { get; set; }
    public int CompanionCount { get; set; }
    public bool HasSeat { get; set; }
    public string MealCategory { get; set; } = "全餐";
    public List<CompanionDto>? Companions { get; set; }
    public string? Remark { get; set; }
    public string Status { get; set; } = string.Empty;
    public string CheckInStatus { get; set; } = "未签到";
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
}

/// <summary>
/// 参会人员列表项DTO
/// </summary>
public class AttendeeListItemDto
{
    public long Id { get; set; }
    public long EventId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Gender { get; set; }
    public string? Phone { get; set; }
    public string? Organization { get; set; }
    public string? Title { get; set; }
    public string? Role { get; set; }
    public bool NeedPickup { get; set; }
    public bool NeedAccommodation { get; set; }
    public long? PrimaryGuestId { get; set; }
    public string? Relation { get; set; }
    public bool IsChild { get; set; }
    public int? Age { get; set; }
    public string? Camp { get; set; }
    public string? GuestType { get; set; }
    public int CompanionCount { get; set; }
    public bool HasSeat { get; set; }
    public string MealCategory { get; set; } = "全餐";
    public List<CompanionDto>? Companions { get; set; }
    public string Status { get; set; } = string.Empty;
    public string CheckInStatus { get; set; } = "未签到";
    public DateTime? ArrivalTime { get; set; }
    public DateTime? DepartureTime { get; set; }
}

/// <summary>
/// 创建参会人请求
/// </summary>
public class CreateAttendeeRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Gender { get; set; }
    public string? Phone { get; set; }
    public string? Organization { get; set; }
    public string? Title { get; set; }
    public string? Role { get; set; }
    public string? DietPreference { get; set; }
    public string? ArrivalMode { get; set; }
    public string? ArrivalFlightTrain { get; set; }
    public DateTime? ArrivalTime { get; set; }
    public string? ArrivalStation { get; set; }
    public string? DepartureMode { get; set; }
    public string? DepartureFlightTrain { get; set; }
    public DateTime? DepartureTime { get; set; }
    public string? DepartureStation { get; set; }
    public bool NeedPickup { get; set; } = true;
    public bool NeedAccommodation { get; set; } = true;
    public string? PreferredRoomType { get; set; }
    public long? PrimaryGuestId { get; set; }
    public string? Relation { get; set; }
    public bool? IsChild { get; set; }
    public int? Age { get; set; }
    public string? Camp { get; set; }
    public string? GuestType { get; set; }
    public string? Remark { get; set; }
    public string? CheckInStatus { get; set; }
}

/// <summary>
/// 更新参会人请求
/// </summary>
public class UpdateAttendeeRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Gender { get; set; }
    public string? Phone { get; set; }
    public string? Organization { get; set; }
    public string? Title { get; set; }
    public string? Role { get; set; }
    public string? DietPreference { get; set; }
    public string? ArrivalMode { get; set; }
    public string? ArrivalFlightTrain { get; set; }
    public DateTime? ArrivalTime { get; set; }
    public string? ArrivalStation { get; set; }
    public string? DepartureMode { get; set; }
    public string? DepartureFlightTrain { get; set; }
    public DateTime? DepartureTime { get; set; }
    public string? DepartureStation { get; set; }
    public bool NeedPickup { get; set; }
    public bool NeedAccommodation { get; set; }
    public string? PreferredRoomType { get; set; }
    public long? PrimaryGuestId { get; set; }
    public string? Relation { get; set; }
    public bool? IsChild { get; set; }
    public int? Age { get; set; }
    public string? Camp { get; set; }
    public string? GuestType { get; set; }
    public string? Remark { get; set; }
    public string? Status { get; set; }
    public string? CheckInStatus { get; set; }
}

/// <summary>
/// 批量更新状态请求
/// </summary>
public class BatchUpdateStatusRequest
{
    public List<long> AttendeeIds { get; set; } = new();
    public string? CheckInStatus { get; set; }
    public string? Status { get; set; }
}

/// <summary>
/// 更新房型偏好请求
/// </summary>
public class UpdateRoomPreferenceRequest
{
    public string? PreferredRoomType { get; set; }
}

/// <summary>
/// 参会人员查询请求
/// </summary>
public class AttendeeQueryRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Keyword { get; set; }
    public string? Role { get; set; }
    public string? Status { get; set; }
    public string? Organization { get; set; }
    public bool? NeedPickup { get; set; }
    public bool? NeedAccommodation { get; set; }
    public string? Camp { get; set; }
    public string? CheckInStatus { get; set; }
    /// <summary>来往日期筛选：true=已明确, false=未明确</summary>
    public bool? HasClearTravelDate { get; set; }
}

/// <summary>
/// 参会人变更影响分析结果DTO
/// </summary>
public class AttendeeImpactAnalysisDto
{
    /// <summary>受影响的接送任务</summary>
    public List<ImpactItem> AffectedPickupTasks { get; set; } = new();
    /// <summary>受影响的住宿分配</summary>
    public List<ImpactItem> AffectedRoomAssignments { get; set; } = new();
    /// <summary>受影响的餐食安排</summary>
    public List<ImpactItem> AffectedMealPlans { get; set; } = new();
    /// <summary>受影响的桌次座位</summary>
    public List<ImpactItem> AffectedTableSeats { get; set; } = new();
    /// <summary>受影响的日程</summary>
    public List<ImpactItem> AffectedSchedules { get; set; } = new();
}

/// <summary>
/// 影响分析子项
/// </summary>
public class ImpactItem
{
    public long Id { get; set; }
    public string Description { get; set; } = string.Empty;
    /// <summary>影响类型：Remove/Reassign/Update</summary>
    public string ImpactType { get; set; } = string.Empty;
}
