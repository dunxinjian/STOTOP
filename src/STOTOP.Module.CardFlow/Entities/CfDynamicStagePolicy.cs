using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

public class CfDynamicStagePolicy : BaseEntity
{
    public long FFlowVersionId { get; set; }
    public string FPolicyKey { get; set; } = string.Empty;
    public long? FSourceStageDefinitionId { get; set; }
    public string FSourceStageKey { get; set; } = string.Empty;
    public string FPolicyName { get; set; } = string.Empty;
    public string FStrategyType { get; set; } = string.Empty;
    public string? FStrategyConfigJson { get; set; }
    public string? FConditionJson { get; set; }
    public string FTriggerTiming { get; set; } = "afterSourceBeforeRoute";
    public string FInsertPosition { get; set; } = "afterSource";
    public string? FContinuationStageKey { get; set; }
    public int FPriority { get; set; }
    public int FMaxInsertCount { get; set; } = 20;
    public string? FFallbackJson { get; set; }
    public string FStatus { get; set; } = "active";
}
