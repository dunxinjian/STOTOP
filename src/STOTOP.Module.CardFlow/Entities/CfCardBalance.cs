using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

public class CfCardBalance : BaseEntity, IOrgScoped, ITenantScoped
{
    public long FCardId { get; set; }
    public decimal FOriginalAmount { get; set; }
    public decimal FOffsetAmount { get; set; }
    public decimal FRemainingAmount { get; set; }
    public string FStatus { get; set; } = "active";
    public long FOrgId { get; set; }
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    public DateTime? FUpdatedTime { get; set; }
    public byte[] FRowVersion { get; set; } = Array.Empty<byte>();
}
