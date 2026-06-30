using STOTOP.Core.Models;

namespace STOTOP.Module.Points.Entities;

public class PmRedeemRecord : BaseEntity, IOrgScoped, ITenantScoped
{
    public long FOrgId { get; set; }
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    public long FUserId { get; set; }
    public long FItemId { get; set; }
    public int FDeductedPoints { get; set; }
    public int FStatus { get; set; }
    public long? FIssuerId { get; set; }
    public DateTime? FIssueTime { get; set; }
    public string? FRemark { get; set; }
    public DateTime FCreateTime { get; set; } = DateTime.Now;
}
