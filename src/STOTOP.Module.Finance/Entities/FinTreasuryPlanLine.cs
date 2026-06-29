using STOTOP.Core.Models;

namespace STOTOP.Module.Finance.Entities;

public class FinTreasuryPlanLine : BaseEntity, IOrgScoped
{
    public long FAccountSetId { get; set; }
    public long FOrgId { get; set; }
    public DateTime FPlanDate { get; set; }
    public DateTime FWeekStartDate { get; set; }
    public string FDirection { get; set; } = "outflow";
    public string FCashCategory { get; set; } = "other";
    public decimal FAmount { get; set; }
    public decimal FProbability { get; set; } = 100m;
    public string FSourceType { get; set; } = "manual";
    public long? FSourceId { get; set; }
    public string? FCounterpartyName { get; set; }
    public string? FRemark { get; set; }
    public DateTime FCreatedTime { get; set; }
    public DateTime FUpdatedTime { get; set; }
}
