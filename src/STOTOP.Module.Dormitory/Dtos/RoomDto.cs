namespace STOTOP.Module.Dormitory.Dtos;

/// <summary>
/// 房间详情 DTO（包含床位列表）
/// </summary>
public class RoomDto
{
    public long Id { get; set; }
    public long BuildingId { get; set; }
    public string BuildingName { get; set; } = string.Empty;
    public int Floor { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public int BedsCount { get; set; }
    public string? RoomType { get; set; }
    public string? Remark { get; set; }
    public int Status { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
    public List<BedDto> Beds { get; set; } = new();
}

/// <summary>
/// 房间列表项 DTO（不含床位列表）
/// </summary>
public class RoomListItemDto
{
    public long Id { get; set; }
    public long BuildingId { get; set; }
    public string BuildingName { get; set; } = string.Empty;
    public int Floor { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public int BedsCount { get; set; }
    public string? RoomType { get; set; }
    public int Status { get; set; }
    public DateTime CreatedTime { get; set; }
    /// <summary>已占用床位数（床位状态=2 已入住）</summary>
    public int OccupiedBeds { get; set; }
}

/// <summary>
/// 创建房间请求
/// </summary>
public class CreateRoomRequest
{
    public int Floor { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public int BedsCount { get; set; } = 4;
    public string? RoomType { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 更新房间请求
/// </summary>
public class UpdateRoomRequest
{
    public int Floor { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public int BedsCount { get; set; } = 4;
    public string? RoomType { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 更新房间状态请求
/// </summary>
public class UpdateRoomStatusRequest
{
    public int Status { get; set; }
}

/// <summary>
/// 房间查询请求
/// </summary>
public class RoomQueryRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Keyword { get; set; }
    public int? Floor { get; set; }
    public int? Status { get; set; }
}
