using STOTOP.Core.Models;

namespace STOTOP.Module.CRM.Entities;

public class CrmBonusPlan : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public string FPeriod { get; set; } = string.Empty;
    public decimal FTotalAmount { get; set; } = 0;
    public string? FCalcRules { get; set; }
    public int FStatus { get; set; } = 0;
    public long? FOaProcessInstanceId { get; set; }
    public string? FCreatorName { get; set; }
    public DateTime FCreatedTime { get; set; } = DateTime.Now;
    public string? FUpdaterName { get; set; }
    public DateTime? FUpdatedTime { get; set; }

    // Navigation
    public List<CrmBonusDetail> Details { get; set; } = new();
}
