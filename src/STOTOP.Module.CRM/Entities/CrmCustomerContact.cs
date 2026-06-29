using STOTOP.Core.Models;

namespace STOTOP.Module.CRM.Entities;

public class CrmCustomerContact : BaseEntity, IOrgScoped
{
    public string FCustomerId { get; set; } = string.Empty;
    public string FName { get; set; } = string.Empty;
    public string? FPhone { get; set; }
    public string? FPosition { get; set; }
    public string? FRoleTag { get; set; }
    public bool FIsPrimary { get; set; } = false;
    public string? FRemark { get; set; }
    public long FOrgId { get; set; }
    public string? FCreatorName { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public string? FUpdaterName { get; set; }
    public DateTime? FUpdatedTime { get; set; }

    // Navigation
    public CrmCustomer Customer { get; set; } = null!;
}
