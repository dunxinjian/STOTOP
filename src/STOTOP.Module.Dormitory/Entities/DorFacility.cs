using STOTOP.Core.Models;

namespace STOTOP.Module.Dormitory.Entities;

/// <summary>
/// 设施登记
/// </summary>
public class DorFacility : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long FRoomId { get; set; }
    public string FFacilityName { get; set; } = string.Empty;
    public int FQuantity { get; set; } = 1;
    public string? FRemark { get; set; }
    public int FStatus { get; set; } = 1;
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;

    // Navigation
    public DorRoom Room { get; set; } = null!;
}
