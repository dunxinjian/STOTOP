using STOTOP.Core.Models;

namespace STOTOP.Module.Conference.Entities;

/// <summary>接送任务</summary>
public class ConfPickupTask : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long FEventId { get; set; }
    public long? FVehicleId { get; set; }
    public string? FType { get; set; }
    public DateTime FDate { get; set; }
    public TimeSpan FTime { get; set; }
    public string? FOrigin { get; set; }
    public string? FDestination { get; set; }
    public string FStatus { get; set; } = "待安排";
    public string? FRemark { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;

    // Navigation
    public ConfEvent Event { get; set; } = null!;
    public ConfVehicle? Vehicle { get; set; }
    public List<ConfPickupPassenger> Passengers { get; set; } = new();
    public List<ConfVehicleSchedule> VehicleSchedules { get; set; } = new();
}
