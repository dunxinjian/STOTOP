using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

public class CfRouteDecisionSnapshot : BaseEntity
{
    public long FCardId { get; set; }
    public long FSourceStageInstanceId { get; set; }
    public long? FFromStageDefinitionId { get; set; }
    public string? FFromStageKey { get; set; }
    public long? FSelectedRouteRuleId { get; set; }
    public string? FSelectedEdgeKey { get; set; }
    public long? FToStageDefinitionId { get; set; }
    public string? FToStageKey { get; set; }
    public string? FCandidateResultsJson { get; set; }
    public string? FDecisionSnapshotJson { get; set; }
    public string? FReason { get; set; }
    public long FOperatorId { get; set; }
    public DateTime FDecisionTime { get; set; }
    public int FRound { get; set; }
}
