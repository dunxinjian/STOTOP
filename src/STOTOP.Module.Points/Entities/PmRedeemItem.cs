using STOTOP.Core.Models;

namespace STOTOP.Module.Points.Entities;

public class PmRedeemItem : BaseEntity, IOrgScoped, ITenantScoped
{
    public string FUID { get; set; } = Guid.NewGuid().ToString("N");
    public long FOrgId { get; set; }
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    public string FName { get; set; } = string.Empty;
    public int FCategory { get; set; }
    public string FDescription { get; set; } = string.Empty;
    public string? FImage { get; set; }
    public int FRequiredPoints { get; set; }
    public int FStock { get; set; } = -1;
    public int FRedeemedCount { get; set; }
    public int FStatus { get; set; } = 1;
    public int FSortOrder { get; set; }
    public DateTime FCreateTime { get; set; } = DateTime.Now;
    public DateTime FUpdateTime { get; set; } = DateTime.Now;
}
