namespace STOTOP.Module.Conference.Dtos;

/// <summary>
/// 车辆详情DTO
/// </summary>
public class VehicleDto
{
    public long Id { get; set; }
    public long EventId { get; set; }
    public string PlateNumber { get; set; } = string.Empty;
    public string? VehicleType { get; set; }
    public int SeatCount { get; set; }
    public string? DriverName { get; set; }
    public string? DriverPhone { get; set; }
    public string? Source { get; set; }
    public string? Remark { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
}

/// <summary>
/// 车辆列表项DTO
/// </summary>
public class VehicleListItemDto
{
    public long Id { get; set; }
    public long EventId { get; set; }
    public string PlateNumber { get; set; } = string.Empty;
    public string? VehicleType { get; set; }
    public int SeatCount { get; set; }
    public string? DriverName { get; set; }
    public string? DriverPhone { get; set; }
    public string? Source { get; set; }
}

/// <summary>
/// 创建车辆请求
/// </summary>
public class CreateVehicleRequest
{
    public string PlateNumber { get; set; } = string.Empty;
    public string? VehicleType { get; set; }
    public int SeatCount { get; set; }
    public string? DriverName { get; set; }
    public string? DriverPhone { get; set; }
    public string? Source { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 更新车辆请求
/// </summary>
public class UpdateVehicleRequest
{
    public string PlateNumber { get; set; } = string.Empty;
    public string? VehicleType { get; set; }
    public int SeatCount { get; set; }
    public string? DriverName { get; set; }
    public string? DriverPhone { get; set; }
    public string? Source { get; set; }
    public string? Remark { get; set; }
}
