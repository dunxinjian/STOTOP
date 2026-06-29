using STOTOP.Core.Models;

namespace STOTOP.Module.CRM.Entities;

public class CrmCommission : BaseEntity, IOrgScoped
{
    public long FReferralId { get; set; }
    public string FCustomerId { get; set; } = string.Empty;
    public long? FContractId { get; set; }
    public decimal FCommissionAmount { get; set; } = 0;
    public string? FCalcBasis { get; set; }
    public long FApplicantId { get; set; }
    public int FStatus { get; set; } = 0;
    public long? FOaProcessInstanceId { get; set; }
    public long? FPaymentOrderId { get; set; }
    public long FOrgId { get; set; }
    public string? FCreatorName { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public string? FUpdaterName { get; set; }
    public DateTime? FUpdatedTime { get; set; }

    // Navigation
    public CrmReferral Referral { get; set; } = null!;
    public CrmCustomer Customer { get; set; } = null!;
}
