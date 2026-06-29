namespace STOTOP.Module.Conference.Dtos;

/// <summary>
/// 随行人员简化DTO
/// </summary>
public class CompanionDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Relation { get; set; }
    public bool IsChild { get; set; }
    public int? Age { get; set; }
    public bool HasSeat { get; set; }
    public string MealCategory { get; set; } = "全餐";
}

/// <summary>
/// 司机通知消息DTO
/// </summary>
public class DriverNotificationDto
{
    public long DriverVehicleId { get; set; }
    public string DriverName { get; set; } = string.Empty;
    public string PlateNumber { get; set; } = string.Empty;
    public string DriverPhone { get; set; } = string.Empty;
    public int TaskCount { get; set; }
    public int PassengerCount { get; set; }
    public string Message { get; set; } = string.Empty;
}
