using STOTOP.Core.Models;

namespace STOTOP.Module.Finance.Entities;

public class FinBudgetOccupation : BaseEntity, IOrgScoped
{
    public long FBudgetVersionId { get; set; }
    public long? FBudgetLineId { get; set; }
    public string FSourceType { get; set; } = "cardflow_card";
    public long FSourceId { get; set; }
    public long FOrgId { get; set; }
    public string FPeriod { get; set; } = string.Empty;
    public string? FAccountCode { get; set; }
    public long? FPLItemId { get; set; }
    public decimal FAmount { get; set; }
    public string FStatus { get; set; } = "occupied";
    public string? FTransitionKey { get; set; }
    public DateTime FCreatedTime { get; set; }
    public DateTime FUpdatedTime { get; set; }
}
