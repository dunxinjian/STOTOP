using STOTOP.Core.Models;

namespace STOTOP.Module.Finance.Entities;

public class FinBudgetVersion : BaseEntity, IOrgScoped
{
    public long FAccountSetId { get; set; }
    public string FName { get; set; } = string.Empty;
    public string FScenarioType { get; set; } = "annual_budget";
    public int FYear { get; set; }
    public long? FBaseVersionId { get; set; }
    public string FStatus { get; set; } = "draft";
    public long FOwnerOrgId { get; set; }
    public long FOrgId { get; set; }
    public string? FCreatedBy { get; set; }
    public DateTime FCreatedTime { get; set; }
    public string? FApprovedBy { get; set; }
    public DateTime? FApprovedTime { get; set; }
    public DateTime FUpdatedTime { get; set; }
}
