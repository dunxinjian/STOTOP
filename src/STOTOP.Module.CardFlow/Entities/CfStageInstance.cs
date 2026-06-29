using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

public class CfStageInstance : BaseEntity
{
    public long FCardId { get; set; }
    public long? FStageDefinitionId { get; set; }
    public string FStageName { get; set; } = string.Empty;
    public string FType { get; set; } = "human";
    public string FApprovalMode { get; set; } = "single";
    public int FRound { get; set; }
    public string FStatus { get; set; } = "pending";
    public DateTime? FStartTime { get; set; }
    public DateTime? FCompletedTime { get; set; }
    public DateTime? FActivatedTime { get; set; }
    public string? FFinalAction { get; set; }
    public string? FOpinion { get; set; }
    public string? FSupplementDataJson { get; set; }
    public bool FIsDynamicInsert { get; set; }
    public long? FInsertSourceStageId { get; set; }
    public string? FInsertContextJson { get; set; }
    public bool FIsTimeout { get; set; }
    public byte[] FRowVersion { get; set; } = Array.Empty<byte>();
}
