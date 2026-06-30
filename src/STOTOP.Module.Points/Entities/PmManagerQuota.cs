using STOTOP.Core.Models;

namespace STOTOP.Module.Points.Entities;

public class PmManagerQuota : BaseEntity, IOrgScoped, ITenantScoped
{
    public long FOrgId { get; set; }
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    public long FManagerId { get; set; }
    public string FYearMonth { get; set; } = string.Empty;
    public int FAwardQuota { get; set; }
    public int FDeductQuota { get; set; }
    public int FUsedAward { get; set; }
    public int FUsedDeduct { get; set; }
    public int FStatus { get; set; }
    public DateTime FCreateTime { get; set; } = DateTime.Now;
}
