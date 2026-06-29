namespace STOTOP.Module.Conference.Dtos;

/// <summary>
/// 酒店详情DTO
/// </summary>
public class HotelDto
{
    public long Id { get; set; }
    public long EventId { get; set; }
    public string HotelName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Contact { get; set; }
    public string? ContactPhone { get; set; }
    public string? AgreedPrice { get; set; }
    public string? Remark { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
    /// <summary>房间列表</summary>
    public List<RoomDto> Rooms { get; set; } = new();
}

/// <summary>
/// 酒店列表项DTO
/// </summary>
public class HotelListItemDto
{
    public long Id { get; set; }
    public long EventId { get; set; }
    public string HotelName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Contact { get; set; }
    public string? ContactPhone { get; set; }
    public string? AgreedPrice { get; set; }
    /// <summary>总房间数</summary>
    public int TotalRooms { get; set; }
    /// <summary>已分配房间数</summary>
    public int AssignedRooms { get; set; }
}

/// <summary>
/// 创建酒店请求
/// </summary>
public class CreateHotelRequest
{
    public string HotelName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Contact { get; set; }
    public string? ContactPhone { get; set; }
    public string? AgreedPrice { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 更新酒店请求
/// </summary>
public class UpdateHotelRequest
{
    public string HotelName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Contact { get; set; }
    public string? ContactPhone { get; set; }
    public string? AgreedPrice { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 房间详情DTO
/// </summary>
public class RoomDto
{
    public long Id { get; set; }
    public long HotelId { get; set; }
    public string? RoomNumber { get; set; }
    public string? RoomType { get; set; }
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Remark { get; set; }
    public DateTime UpdatedTime { get; set; }
    /// <summary>入住人员列表</summary>
    public List<AttendeeListItemDto> Guests { get; set; } = new();
}

/// <summary>
/// 房间列表项DTO
/// </summary>
public class RoomListItemDto
{
    public long Id { get; set; }
    public long HotelId { get; set; }
    public string? RoomNumber { get; set; }
    public string? RoomType { get; set; }
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public string Status { get; set; } = string.Empty;
    /// <summary>入住人数</summary>
    public int GuestCount { get; set; }
    /// <summary>入住人姓名（逗号分隔）</summary>
    public string? GuestNames { get; set; }
}

/// <summary>
/// 批量添加房间请求
/// </summary>
public class BatchAddRoomRequest
{
    public List<RoomInput> Rooms { get; set; } = new();
}

/// <summary>
/// 房间输入项
/// </summary>
public class RoomInput
{
    public string? RoomNumber { get; set; }
    public string? RoomType { get; set; }
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 更新房间请求
/// </summary>
public class UpdateRoomRequest
{
    public string? RoomNumber { get; set; }
    public string? RoomType { get; set; }
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 分配入住人员请求
/// </summary>
public class RoomAssignRequest
{
    /// <summary>参会人ID列表</summary>
    public List<long> AttendeeIds { get; set; } = new();
}

/// <summary>
/// 智能分房预览DTO
/// </summary>
public class AutoAssignPreviewDto
{
    /// <summary>分配方案</summary>
    public List<RoomAssignmentPreviewItem> Assignments { get; set; } = new();
    /// <summary>无法分配的人员</summary>
    public List<UnassignedAttendeeItem> UnassignedAttendees { get; set; } = new();
    /// <summary>满足率</summary>
    public double SatisfactionRate { get; set; }
}

/// <summary>
/// 房间分配预览子项
/// </summary>
public class RoomAssignmentPreviewItem
{
    public long RoomId { get; set; }
    public string? RoomNumber { get; set; }
    public string? RoomType { get; set; }
    public string? HotelName { get; set; }
    public List<AssignedGuestItem> Guests { get; set; } = new();
}

/// <summary>
/// 分配的入住人
/// </summary>
public class AssignedGuestItem
{
    public long AttendeeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Gender { get; set; }
    public string? Organization { get; set; }
    public string? Role { get; set; }
}

/// <summary>
/// 未分配人员项
/// </summary>
public class UnassignedAttendeeItem
{
    public long AttendeeId { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>无法分配原因</summary>
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// 住宿需求统计DTO
/// </summary>
public class AccommodationDemandStatsDto
{
    public List<DailyDemandItem> DailyStats { get; set; } = new();
    public Dictionary<string, int> TotalByRoomType { get; set; } = new();
    public int TotalNeedAccommodation { get; set; }
}

/// <summary>
/// 每日需求统计项
/// </summary>
public class DailyDemandItem
{
    public DateTime Date { get; set; }
    public Dictionary<string, RoomTypeStat> RoomTypes { get; set; } = new();
    public int TotalDemand { get; set; }
}

/// <summary>
/// 房型统计
/// </summary>
public class RoomTypeStat
{
    /// <summary>需求间数</summary>
    public int Demand { get; set; }
    /// <summary>已分配间数</summary>
    public int Allocated { get; set; }
    /// <summary>可用间数</summary>
    public int Available { get; set; }
}

/// <summary>
/// 房型人员查询DTO
/// </summary>
public class RoomTypeGuestDto
{
    public long AttendeeId { get; set; }
    public string? Name { get; set; }
    public string? Gender { get; set; }
    public string? Organization { get; set; }
    public string? Phone { get; set; }
    public string? PreferredRoomType { get; set; }
    public string? RoomNumber { get; set; }
    public string? HotelName { get; set; }
    /// <summary>主宾客姓名（随行人员时显示对应主宾客名，主宾客自身为空）</summary>
    public string? PrimaryGuestName { get; set; }
    /// <summary>与主宾客的关系（配偶/子女/父母等）</summary>
    public string? Relation { get; set; }
}
