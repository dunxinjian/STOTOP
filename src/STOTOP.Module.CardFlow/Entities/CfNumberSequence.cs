using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

public class CfNumberSequence : BaseEntity
{
    public long FFlowDefinitionId { get; set; }
    public long FOrgId { get; set; }
    public int FYear { get; set; }
    public int FCurrentSequence { get; set; }
    public DateTime? FUpdatedTime { get; set; }
    public byte[] FRowVersion { get; set; } = Array.Empty<byte>();
}
