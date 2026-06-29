using STOTOP.Core.Models;

namespace STOTOP.Module.CRM.Entities;

public class CrmWaybillPool : BaseEntity, IOrgScoped
{
    public string FBrandCode { get; set; } = string.Empty;
    public string? FPrefix { get; set; }
    public string FStartNo { get; set; } = string.Empty;
    public string FEndNo { get; set; } = string.Empty;
    public int FTotalCount { get; set; } = 0;
    public int FAllocatedCount { get; set; } = 0;
    public int FRemainingCount { get; set; } = 0;
    public DateOnly? FPurchaseDate { get; set; }
    public decimal FUnitPrice { get; set; } = 0;
    public int FVersion { get; set; } = 0;
    public int FStatus { get; set; } = 0;
    public long FOrgId { get; set; }
    public string? FCreatorName { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public string? FUpdaterName { get; set; }
    public DateTime? FUpdatedTime { get; set; }

    // Navigation
    public List<CrmWaybillAllocation> Allocations { get; set; } = new();
}
