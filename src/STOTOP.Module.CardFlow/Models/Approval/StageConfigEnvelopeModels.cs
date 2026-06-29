using STOTOP.Module.CardFlow.Models.Schema;

namespace STOTOP.Module.CardFlow.Models.Approval;

public sealed class StageConfigEnvelope
{
    public int Version { get; set; } = 1;
    public List<string> InputFields { get; set; } = new();
    public StageViewProfile? ViewProfile { get; set; }
    public StageActionPolicy? ActionPolicy { get; set; }
    public ApprovalModeConfig? ApprovalMode { get; set; }
    public List<string> Warnings { get; set; } = new();
}
