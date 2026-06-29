using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

public class CfActionLog : BaseEntity
{
    public long FCardId { get; set; }
    public long? FStageInstanceId { get; set; }
    public string FActionType { get; set; } = string.Empty;
    public long FOperatorId { get; set; }
    public string FOperatorName { get; set; } = string.Empty;
    public DateTime FOperationTime { get; set; }
    public string? FOpinion { get; set; }
    public string? FDetailJson { get; set; }
}
