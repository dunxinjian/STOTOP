using STOTOP.Core.Models;

namespace STOTOP.Module.Task.Entities;

public class TmProgressReport : BaseEntity, IOrgScoped, ITenantScoped
{
    public long FOrgId { get; set; }
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    public long FTaskId { get; set; }
    public long FReporterId { get; set; }
    public int FProgress { get; set; }
    public string FContent { get; set; } = string.Empty;
    public decimal? FHours { get; set; }
    public bool FPushedToDingTalk { get; set; } = false;
    public DateTime FCreateTime { get; set; } = DateTime.Now;

    // 导航属性
    public virtual TmTask? Task { get; set; }
}
