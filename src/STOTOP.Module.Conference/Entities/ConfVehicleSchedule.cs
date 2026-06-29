using STOTOP.Core.Models;

namespace STOTOP.Module.Conference.Entities;

/// <summary>车辆日程</summary>
public class ConfVehicleSchedule : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long FEventId { get; set; }
    public long FVehicleId { get; set; }
    public DateTime FDate { get; set; }
    public TimeSpan FStartTime { get; set; }
    public TimeSpan FEndTime { get; set; }
    public string? FTaskType { get; set; }
    public long? FPickupTaskId { get; set; }
    public string? FOrigin { get; set; }
    public string? FDestination { get; set; }
    public int FPassengerCount { get; set; }
    public string? FRemark { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;

    // Navigation
    public ConfEvent Event { get; set; } = null!;
    public ConfVehicle Vehicle { get; set; } = null!;
    public ConfPickupTask? PickupTask { get; set; }
}
