using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

public class CfDelegation : BaseEntity, IOrgScoped
{
    public long FDelegatorId { get; set; }
    public string FDelegatorName { get; set; } = string.Empty;
    public long FTrusteeId { get; set; }
    public string FTrusteeName { get; set; } = string.Empty;
    public DateTime FStartTime { get; set; }
    public DateTime FEndTime { get; set; }
    public string? FApplicableFlowsJson { get; set; }
    public string FStatus { get; set; } = "active";
    public long FOrgId { get; set; }
}
