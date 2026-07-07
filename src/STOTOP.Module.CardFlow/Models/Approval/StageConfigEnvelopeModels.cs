using STOTOP.Module.CardFlow.Models.Schema;

namespace STOTOP.Module.CardFlow.Models.Approval;

public sealed class StageConfigEnvelope
{
    public int Version { get; set; } = 1;
    public List<string> InputFields { get; set; } = new();
    public StageViewProfile? ViewProfile { get; set; }
    public StageActionPolicy? ActionPolicy { get; set; }
    public ApprovalModeConfig? ApprovalMode { get; set; }
    public StageAutoDecision? AutoDecision { get; set; }
    public List<string> Warnings { get; set; } = new();
}

/// <summary>人工节点自动裁决配置：激活时条件命中则自动通过/自动拒绝（Mode: none|autoApprove|autoReject）。</summary>
public sealed class StageAutoDecision
{
    public string Mode { get; set; } = "none";
    public string? ConditionJson { get; set; }
}
