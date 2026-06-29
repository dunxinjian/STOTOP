namespace STOTOP.Module.Vehicle.Dtos;

/// <summary>
/// 车辆位置信息 DTO
/// </summary>
public class VehicleLocationDto
{
    public long VehicleId { get; set; }
    public string? VehicleCode { get; set; }
    public double Longitude { get; set; }       // 经度
    public double Latitude { get; set; }        // 纬度
    public double? Speed { get; set; }          // 速度 km/h
    public DateTime? ReportTime { get; set; }   // 上报时间
    public string? Address { get; set; }        // 逆地理编码地址
}

/// <summary>
/// 车辆轨迹点 DTO
/// </summary>
public class VehicleTrackPointDto
{
    public double Longitude { get; set; }
    public double Latitude { get; set; }
    public double? Speed { get; set; }
    public DateTime ReportTime { get; set; }
}

/// <summary>
/// 车辆轨迹查询请求
/// </summary>
public class VehicleTrackQueryRequest
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
}
