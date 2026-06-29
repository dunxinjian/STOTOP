using STOTOP.Core.Models;

namespace STOTOP.Module.CRM.Entities;

public class CrmWaybillAllocation : BaseEntity, IOrgScoped
{
    public long FPrepaymentId { get; set; }
    public string FCustomerId { get; set; } = string.Empty;
    public long FPoolId { get; set; }
    public string FStartNo { get; set; } = string.Empty;
    public string FEndNo { get; set; } = string.Empty;
    public int FAllocatedCount { get; set; } = 0;
    public DateOnly FAllocationDate { get; set; }
    public long FOperatorId { get; set; }
    public int FStatus { get; set; } = 1;
    public long FOrgId { get; set; }
    public string? FCreatorName { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public string? FUpdaterName { get; set; }
    public DateTime? FUpdatedTime { get; set; }

    // Navigation
    public CrmPrepayment Prepayment { get; set; } = null!;
    public CrmCustomer Customer { get; set; } = null!;
    public CrmWaybillPool Pool { get; set; } = null!;
}
