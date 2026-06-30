using STOTOP.Core.Models;

namespace STOTOP.Module.System.Entities;

public class SysFeedbackActivity : BaseEntity, IOrgScoped, ITenantScoped
{
    public long FOrgId { get; set; }
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    public long FFeedbackId { get; set; }
    public long FActorId { get; set; }
    public string FAction { get; set; } = string.Empty;
    public string? FContent { get; set; }
    public int? FFromStatus { get; set; }
    public int? FToStatus { get; set; }
    public DateTime FCreateTime { get; set; } = DateTime.Now;
}
