namespace STOTOP.Module.Vehicle.Dtos;

/// <summary>
/// 车辆详情 DTO（含分配、保险、维修数量统计）
/// </summary>
public class VehicleDto
{
    public long Id { get; set; }
    public string Uid { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? PlateNumber { get; set; }
    public string? Brand { get; set; }
    public string? FrameNumber { get; set; }
    public int OwnershipType { get; set; }
    public long? OwnerId { get; set; }
    public string? OwnerName { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public decimal? PurchasePrice { get; set; }
    public int VehicleStatus { get; set; }
    public string? Color { get; set; }
    public string? GpsDeviceNo { get; set; }
    public string? Image { get; set; }
    public string? Remark { get; set; }
    public int Status { get; set; }
    public long? CreatorId { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
    // 统计信息
    public int AssignmentCount { get; set; }
    public int MaintenanceCount { get; set; }
    // 当前分配信息
    public VehicleAssignmentDto? CurrentAssignment { get; set; }
}

/// <summary>
/// 车辆列表项 DTO
/// </summary>
public class VehicleListItemDto
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? PlateNumber { get; set; }
    public string? Brand { get; set; }
    public int OwnershipType { get; set; }
    public string? OwnerName { get; set; }
    public int VehicleStatus { get; set; }
    public int Status { get; set; }
    public DateTime CreatedTime { get; set; }
}

/// <summary>
/// 创建车辆请求
/// </summary>
public class CreateVehicleRequest
{
    public string Code { get; set; } = string.Empty;
    public string? PlateNumber { get; set; }
    public string? Brand { get; set; }
    public string? FrameNumber { get; set; }
    public int OwnershipType { get; set; } = 1;
    public long? OwnerId { get; set; }
    public string? OwnerName { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public decimal? PurchasePrice { get; set; }
    public string? Color { get; set; }
    public string? GpsDeviceNo { get; set; }
    public string? Image { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 更新车辆请求
/// </summary>
public class UpdateVehicleRequest
{
    public string Code { get; set; } = string.Empty;
    public string? PlateNumber { get; set; }
    public string? Brand { get; set; }
    public string? FrameNumber { get; set; }
    public int OwnershipType { get; set; } = 1;
    public long? OwnerId { get; set; }
    public string? OwnerName { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public decimal? PurchasePrice { get; set; }
    public int VehicleStatus { get; set; } = 1;
    public string? Color { get; set; }
    public string? GpsDeviceNo { get; set; }
    public string? Image { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 车辆查询请求
/// </summary>
public class VehicleQueryRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Keyword { get; set; }
    public int? OwnershipType { get; set; }
    public int? VehicleStatus { get; set; }
    public int? Status { get; set; }
}

/// <summary>
/// 车辆统计 DTO（按权属和状态分组）
/// </summary>
public class VehicleStatisticsDto
{
    public int TotalCount { get; set; }
    public int IdleCount { get; set; }
    public int InUseCount { get; set; }
    public int MaintenanceCount { get; set; }
    public int ScrapCount { get; set; }
    public int CompanyOwnedCount { get; set; }
    public int PersonalOwnedCount { get; set; }
    public List<VehicleStatusGroupDto> ByStatus { get; set; } = new();
    public List<VehicleOwnershipGroupDto> ByOwnership { get; set; } = new();
}

/// <summary>
/// 车辆状态分组统计
/// </summary>
public class VehicleStatusGroupDto
{
    public int VehicleStatus { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public int Count { get; set; }
}

/// <summary>
/// 车辆权属分组统计
/// </summary>
public class VehicleOwnershipGroupDto
{
    public int OwnershipType { get; set; }
    public string OwnershipName { get; set; } = string.Empty;
    public int Count { get; set; }
}
