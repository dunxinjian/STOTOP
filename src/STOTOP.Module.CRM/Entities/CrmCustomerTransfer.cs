using STOTOP.Core.Models;

namespace STOTOP.Module.CRM.Entities;

public class CrmCustomerTransfer : BaseEntity, IOrgScoped
{
    public string FCustomerId { get; set; } = string.Empty;
    public int FTransferType { get; set; }
    public long? FOriginalOrgId { get; set; }
    public long? FNewOrgId { get; set; }
    public long? FOriginalBdEmployeeId { get; set; }
    public long? FNewBdEmployeeId { get; set; }
    public int? FOriginalStatus { get; set; }
    public int? FNewStatus { get; set; }
    public string? FReason { get; set; }
    public long? FOperatorId { get; set; }
    public long FOrgId { get; set; }
    public string? FCreatorName { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public string? FUpdaterName { get; set; }
    public DateTime? FUpdatedTime { get; set; }

    // Navigation
    public CrmCustomer Customer { get; set; } = null!;
}
