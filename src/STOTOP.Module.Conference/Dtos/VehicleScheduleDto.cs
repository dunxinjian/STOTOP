namespace STOTOP.Module.Conference.Dtos;

/// <summary>
/// 车辆日程详情DTO
/// </summary>
public class VehicleScheduleDto
{
    public long Id { get; set; }
    public long EventId { get; set; }
    public long VehicleId { get; set; }
    public string? VehiclePlateNumber { get; set; }
    public string? DriverName { get; set; }
    public string? DriverPhone { get; set; }
    public DateTime Date { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string? TaskType { get; set; }
    public long? PickupTaskId { get; set; }
    public string? Origin { get; set; }
    public string? Destination { get; set; }
    public int PassengerCount { get; set; }
    public string? Remark { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
}

/// <summary>
/// 车辆日程列表项DTO
/// </summary>
public class VehicleScheduleListItemDto
{
    public long Id { get; set; }
    public long VehicleId { get; set; }
    public string? VehiclePlateNumber { get; set; }
    public string? DriverName { get; set; }
    public DateTime Date { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string? TaskType { get; set; }
    public string? Origin { get; set; }
    public string? Destination { get; set; }
    public int PassengerCount { get; set; }
}

/// <summary>
/// 手动添加用车任务请求
/// </summary>
public class AddVehicleTaskRequest
{
    public long VehicleId { get; set; }
    public DateTime Date { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    /// <summary>任务类型：活动用车/待命/其他</summary>
    public string TaskType { get; set; } = string.Empty;
    public string? Origin { get; set; }
    public string? Destination { get; set; }
    public int PassengerCount { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 车辆日程自动推导预览DTO
/// </summary>
public class VehicleScheduleGeneratePreviewDto
{
    /// <summary>将要生成的日程项</summary>
    public List<VehicleSchedulePreviewItem> ScheduleItems { get; set; } = new();
    /// <summary>时间冲突告警</summary>
    public List<string> Conflicts { get; set; } = new();
}

/// <summary>
/// 车辆日程预览子项
/// </summary>
public class VehicleSchedulePreviewItem
{
    public long VehicleId { get; set; }
    public string? VehiclePlateNumber { get; set; }
    public DateTime Date { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string? TaskType { get; set; }
    public string? Origin { get; set; }
    public string? Destination { get; set; }
    public int PassengerCount { get; set; }
}

/// <summary>
/// 司机卡数据DTO
/// </summary>
public class DriverCardDto
{
    public long VehicleId { get; set; }
    public string PlateNumber { get; set; } = string.Empty;
    public string? VehicleType { get; set; }
    public string? DriverName { get; set; }
    public string? DriverPhone { get; set; }
    public DateTime Date { get; set; }
    /// <summary>当日任务列表</summary>
    public List<DriverTaskItem> Tasks { get; set; } = new();
    /// <summary>当日总工作时长(分钟)</summary>
    public int TotalWorkMinutes { get; set; }
    /// <summary>当日总行程数</summary>
    public int TotalTrips { get; set; }
}

/// <summary>
/// 司机任务子项
/// </summary>
public class DriverTaskItem
{
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string? TaskType { get; set; }
    public string? Origin { get; set; }
    public string? Destination { get; set; }
    /// <summary>乘客名单（逗号分隔）</summary>
    public string? PassengerNames { get; set; }
    public string? Remark { get; set; }
}
