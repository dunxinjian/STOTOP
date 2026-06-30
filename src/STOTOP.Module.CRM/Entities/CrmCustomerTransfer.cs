using STOTOP.Core.Models;

namespace STOTOP.Module.CRM.Entities;

public class CrmCustomerTransfer : BaseEntity, IOrgScoped, ITenantScoped
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
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    public string? FCreatorName { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public string? FUpdaterName { get; set; }
    public DateTime? FUpdatedTime { get; set; }

    // Navigation
    public CrmCustomer Customer { get; set; } = null!;
}
