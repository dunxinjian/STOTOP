using STOTOP.Core.Models;

namespace STOTOP.Module.CRM.Entities;

public class CrmExternalContact : BaseEntity, IOrgScoped
{
    public string FName { get; set; } = string.Empty;
    public string? FPhone { get; set; }
    public string? FCompany { get; set; }
    public string? FBankAccount { get; set; }
    public string? FBankName { get; set; }
    public string? FRemark { get; set; }
    public int FStatus { get; set; } = 0;
    public long FOrgId { get; set; }
    public string? FCreatorName { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public string? FUpdaterName { get; set; }
    public DateTime? FUpdatedTime { get; set; }

    // Navigation
    public List<CrmReferral> Referrals { get; set; } = new();
}
