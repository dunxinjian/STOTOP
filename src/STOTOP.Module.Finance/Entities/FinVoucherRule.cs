using STOTOP.Core.Models;

namespace STOTOP.Module.Finance.Entities;

/// <summary>
/// FIN凭证手动规则
/// </summary>
public class FinVoucherRule : BaseEntity
{
    public string FRuleName { get; set; } = string.Empty;
    public long? FChannelId { get; set; }
    public string? FMatchCondition { get; set; }
    public string? FDebitAccount { get; set; }
    public string? FCreditAccount { get; set; }
    public string? FSummaryTemplate { get; set; }
    public int FPriority { get; set; }
    public int FStatus { get; set; } = 1;
    public string? FCreatorName { get; set; }
    public DateTime FCreatedTime { get; set; }
    public string? FUpdaterName { get; set; }
    public DateTime? FUpdatedTime { get; set; }
}
