using STOTOP.Core.Models;

namespace STOTOP.Module.Conference.Entities;

/// <summary>物品</summary>
public class ConfMaterial : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long FEventId { get; set; }
    public string FName { get; set; } = string.Empty;
    public string? FCategory { get; set; }
    public string? FSpecification { get; set; }
    public int FRequiredQuantity { get; set; }
    public int FReceivedQuantity { get; set; }
    public string? FUnit { get; set; }
    public string? FAcquisitionMethod { get; set; }
    public decimal FUnitPrice { get; set; }
    public decimal FTotalPrice { get; set; }
    public string? FSupplier { get; set; }
    public string? FSupplierContact { get; set; }
    public DateTime? FRequiredDate { get; set; }
    public DateTime? FReceivedDate { get; set; }
    public DateTime? FReturnDate { get; set; }
    public string FStatus { get; set; } = "计划中";
    public string? FResponsible { get; set; }
    public long? FScheduleId { get; set; }
    public string? FRemark { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public DateTime FUpdatedTime { get; set; } = DateTime.Now;

    // Navigation
    public ConfEvent Event { get; set; } = null!;
    public ConfSchedule? Schedule { get; set; }
}
