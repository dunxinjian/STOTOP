using STOTOP.Core.Models;

namespace STOTOP.Module.Conference.Entities;

/// <summary>车辆</summary>
public class ConfVehicle : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long FEventId { get; set; }
    public string FPlateNumber { get; set; } = string.Empty;
    public string? FVehicleType { get; set; }
    public int FSeatCount { get; set; }
    public string? FDriverName { get; set; }
    public string? FDriverPhone { get; set; }
    public string? FSource { get; set; }
    public string? FRemark { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;

    // Navigation
    public ConfEvent Event { get; set; } = null!;
    public List<ConfPickupTask> PickupTasks { get; set; } = new();
    public List<ConfVehicleSchedule> VehicleSchedules { get; set; } = new();
}
