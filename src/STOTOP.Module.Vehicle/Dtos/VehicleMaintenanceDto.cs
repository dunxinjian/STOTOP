namespace STOTOP.Module.Vehicle.Dtos;

/// <summary>
/// 维修详情 DTO
/// </summary>
public class VehicleMaintenanceDto
{
    public long Id { get; set; }
    public string Uid { get; set; } = string.Empty;
    public long VehicleId { get; set; }
    public DateTime MaintenanceDate { get; set; }
    public string? MaintenanceType { get; set; }
    public string MaintenanceItem { get; set; } = string.Empty;
    public string? MaintenanceUnit { get; set; }
    public decimal? MaintenanceCost { get; set; }
    public int CostBearer { get; set; }
    public DateTime? CompletionDate { get; set; }
    public int MaintenanceStatus { get; set; }
    public string? Remark { get; set; }
    public long? CreatorId { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
}

/// <summary>
/// 维修列表项 DTO
/// </summary>
public class VehicleMaintenanceListItemDto
{
    public long Id { get; set; }
    public long VehicleId { get; set; }
    public string VehicleCode { get; set; } = string.Empty;
    public DateTime MaintenanceDate { get; set; }
    public string? MaintenanceType { get; set; }
    public string MaintenanceItem { get; set; } = string.Empty;
    public decimal? MaintenanceCost { get; set; }
    public int CostBearer { get; set; }
    public int MaintenanceStatus { get; set; }
}

/// <summary>
/// 创建维修请求
/// </summary>
public class CreateMaintenanceRequest
{
    public long VehicleId { get; set; }
    public DateTime MaintenanceDate { get; set; }
    public string? MaintenanceType { get; set; }
    public string MaintenanceItem { get; set; } = string.Empty;
    public string? MaintenanceUnit { get; set; }
    public decimal? MaintenanceCost { get; set; }
    public int CostBearer { get; set; } = 1;
    public string? Remark { get; set; }
}

/// <summary>
/// 完成维修请求
/// </summary>
public class CompleteMaintenanceRequest
{
    public DateTime CompletionDate { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 维修查询请求
/// </summary>
public class MaintenanceQueryRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public long? VehicleId { get; set; }
    public string? MaintenanceType { get; set; }
    public int? MaintenanceStatus { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}
