using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

public class CfNumberSequence : BaseEntity, ITenantScoped
{
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    public long FFlowDefinitionId { get; set; }
    public long FOrgId { get; set; }
    public int FYear { get; set; }
    public int FCurrentSequence { get; set; }
    public DateTime? FUpdatedTime { get; set; }
    public byte[] FRowVersion { get; set; } = Array.Empty<byte>();
}
