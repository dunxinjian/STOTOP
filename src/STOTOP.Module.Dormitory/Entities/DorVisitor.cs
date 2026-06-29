using STOTOP.Core.Models;

namespace STOTOP.Module.Dormitory.Entities;

/// <summary>
/// 访客登记
/// </summary>
public class DorVisitor : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long FRoomId { get; set; }
    public string FVisitorName { get; set; } = string.Empty;
    public string? FVisitorPhone { get; set; }
    public string? FVisitorIdCard { get; set; }
    public string? FVisitReason { get; set; }
    public long? FVisitedPersonId { get; set; }
    public DateTime FArrivalTime { get; set; }
    public DateTime? FDepartureTime { get; set; }
    public string? FRemark { get; set; }
    public int FStatus { get; set; } = 1;
    public DateTime FCreatedTime { get; set; } = DateTime.Now;

    // Navigation
    public DorRoom Room { get; set; } = null!;
}
