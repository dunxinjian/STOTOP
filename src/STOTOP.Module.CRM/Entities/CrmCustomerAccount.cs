using STOTOP.Core.Models;

namespace STOTOP.Module.CRM.Entities;

public class CrmCustomerAccount : BaseEntity, IOrgScoped
{
    public string FCustomerId { get; set; } = string.Empty;
    public string FBrandCode { get; set; } = string.Empty;
    public decimal FBalance { get; set; } = 0;
    public decimal FTotalRecharge { get; set; } = 0;
    public decimal FTotalConsumption { get; set; } = 0;
    public decimal FFrozenAmount { get; set; } = 0;
    public long FOrgId { get; set; }
    public string? FCreatorName { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public string? FUpdaterName { get; set; }
    public DateTime? FUpdatedTime { get; set; }

    // Navigation
    public CrmCustomer Customer { get; set; } = null!;
    public List<CrmPrepayment> Prepayments { get; set; } = new();
}
