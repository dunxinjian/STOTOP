using STOTOP.Core.Models;

namespace STOTOP.Module.Finance.Entities;

public class FinTrialBalance : BaseEntity
{
    public long FPeriodId { get; set; }
    public long FAccountSetId { get; set; }
    public decimal FTotalDebit { get; set; }
    public decimal FTotalCredit { get; set; }
    public bool FIsBalanced { get; set; }
    public string? FDetails { get; set; }
    public DateTime FGeneratedTime { get; set; }
    public long FOperatorId { get; set; }
}
