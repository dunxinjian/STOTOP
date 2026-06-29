using STOTOP.Core.Models;

namespace STOTOP.Module.CRM.Entities;

public class CrmPrepayment : BaseEntity, IOrgScoped
{
    public string FCustomerId { get; set; } = string.Empty;
    public long FCustomerAccountId { get; set; }
    public long FOrgId { get; set; }
    public string FBrandCode { get; set; } = string.Empty;
    public decimal FPrepayAmount { get; set; } = 0;
    public decimal FReceivedAmount { get; set; } = 0;
    public int FExpectedWaybillCount { get; set; } = 0;
    public int FAllocatedWaybillCount { get; set; } = 0;
    public int FStatus { get; set; } = 0;
    public long? FBankTransactionId { get; set; }
    public string? FRemark { get; set; }
    public string? FCreatorName { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public string? FUpdaterName { get; set; }
    public DateTime? FUpdatedTime { get; set; }

    // Navigation
    public CrmCustomerAccount CustomerAccount { get; set; } = null!;
    public List<CrmWaybillAllocation> Allocations { get; set; } = new();
}
