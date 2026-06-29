using STOTOP.Core.Models;

namespace STOTOP.Module.Dormitory.Entities;

/// <summary>
/// 入住记录
/// </summary>
public class DorResidence : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long FBedId { get; set; }
    public long FEmployeeId { get; set; }
    public DateTime FCheckInDate { get; set; }
    public DateTime? FCheckOutDate { get; set; }
    public string? FRemark { get; set; }
    public int FStatus { get; set; } = 1;
    public long? FCreatorId { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;

    // Navigation
    public DorBed Bed { get; set; } = null!;
}
