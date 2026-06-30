using STOTOP.Core.Models;

namespace STOTOP.Module.Conference.Entities;

/// <summary>车辆日程</summary>
public class ConfVehicleSchedule : BaseEntity, IOrgScoped, ITenantScoped
{
    public long FOrgId { get; set; }
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
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
