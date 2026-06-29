using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

public class CfStageRouteRule : BaseEntity
{
    public long FFlowVersionId { get; set; }
    public string FEdgeKey { get; set; } = string.Empty;
    public long? FFromStageDefinitionId { get; set; }
    public string FFromStageKey { get; set; } = string.Empty;
    public long? FToStageDefinitionId { get; set; }
    public string FToStageKey { get; set; } = string.Empty;
    public string FRouteName { get; set; } = string.Empty;
    public string? FConditionJson { get; set; }
    public int FPriority { get; set; }
    public bool FIsDefault { get; set; }
    public string FStatus { get; set; } = "active";
    public string? FFailurePolicyJson { get; set; }
}
