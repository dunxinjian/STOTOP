using STOTOP.Core.Models;

namespace STOTOP.Module.CRM.Entities;

public class CrmReferral : BaseEntity, IOrgScoped
{
    public string FCustomerId { get; set; } = string.Empty;
    public long FOrgId { get; set; }
    public int FReferrerType { get; set; }
    public long? FEmployeeId { get; set; }
    public long? FExternalContactId { get; set; }
    public DateOnly FReferralDate { get; set; }
    public string? FDescription { get; set; }
    public decimal? FCommissionRate { get; set; }
    public string? FCreatorName { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public string? FUpdaterName { get; set; }
    public DateTime? FUpdatedTime { get; set; }

    // Navigation
    public CrmCustomer Customer { get; set; } = null!;
    public CrmExternalContact? ExternalContact { get; set; }
    public List<CrmCommission> Commissions { get; set; } = new();
}
