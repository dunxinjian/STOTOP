namespace STOTOP.Module.Dormitory.Dtos;

/// <summary>
/// 楼栋详情 DTO（包含房间列表）
/// </summary>
public class BuildingDto
{
    public long Id { get; set; }
    public string Uid { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public int TotalFloors { get; set; }
    public long? ManagerId { get; set; }
    public string? ManagerName { get; set; }
    public string? DormitoryType { get; set; }
    public string? Remark { get; set; }
    public int Status { get; set; }
    public long? CreatorId { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
    public List<RoomDto> Rooms { get; set; } = new();
}

/// <summary>
/// 楼栋列表项 DTO（不含房间列表）
/// </summary>
public class BuildingListItemDto
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public int TotalFloors { get; set; }
    public long? ManagerId { get; set; }
    public string? DormitoryType { get; set; }
    public int Status { get; set; }
    public DateTime CreatedTime { get; set; }
    // 统计（由楼栋下房间/床位实时计算）
    public int RoomCount { get; set; }
    public int BedCount { get; set; }
    public int OccupiedBeds { get; set; }
}

/// <summary>
/// 创建楼栋请求
/// </summary>
public class CreateBuildingRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public int TotalFloors { get; set; } = 1;
    public long? ManagerId { get; set; }
    public string? DormitoryType { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 更新楼栋请求
/// </summary>
public class UpdateBuildingRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public int TotalFloors { get; set; } = 1;
    public long? ManagerId { get; set; }
    public string? DormitoryType { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 更新楼栋状态请求
/// </summary>
public class UpdateBuildingStatusRequest
{
    public int Status { get; set; }
}

/// <summary>
/// 楼栋查询请求
/// </summary>
public class BuildingQueryRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Keyword { get; set; }
    public int? Status { get; set; }
}
