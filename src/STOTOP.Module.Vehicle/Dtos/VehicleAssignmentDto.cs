namespace STOTOP.Module.Vehicle.Dtos;

/// <summary>
/// 车辆分配详情 DTO
/// </summary>
public class VehicleAssignmentDto
{
    public long Id { get; set; }
    public string Uid { get; set; } = string.Empty;
    public long VehicleId { get; set; }
    public string? VehicleCode { get; set; }
    public string? VehiclePlateNumber { get; set; }
    public long EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public int AssignmentType { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int AssignmentStatus { get; set; }
    public string? Remark { get; set; }
    public long? CreatorId { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
}

/// <summary>
/// 车辆分配列表项 DTO
/// </summary>
public class VehicleAssignmentListItemDto
{
    public long Id { get; set; }
    public long VehicleId { get; set; }
    public string? VehicleCode { get; set; }
    public string? VehiclePlateNumber { get; set; }
    public long EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public int AssignmentType { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int AssignmentStatus { get; set; }
    public DateTime CreatedTime { get; set; }
}

/// <summary>
/// 创建车辆分配请求
/// </summary>
public class CreateAssignmentRequest
{
    public long VehicleId { get; set; }
    public long EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public int AssignmentType { get; set; } = 1;
    public DateTime StartDate { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 归还车辆请求
/// </summary>
public class ReturnVehicleRequest
{
    public DateTime EndDate { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 车辆分配查询请求
/// </summary>
public class AssignmentQueryRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public long? VehicleId { get; set; }
    public long? EmployeeId { get; set; }
    public int? AssignmentType { get; set; }
    public int? AssignmentStatus { get; set; }
}
