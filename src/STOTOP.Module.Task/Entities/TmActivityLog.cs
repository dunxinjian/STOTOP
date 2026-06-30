using STOTOP.Core.Models;

namespace STOTOP.Module.Task.Entities;

public class TmActivityLog : BaseEntity, IOrgScoped, ITenantScoped
{
    public long FOrgId { get; set; }
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    public long FTaskId { get; set; }
    public int FActionType { get; set; }
    public string? FOldValue { get; set; }
    public string? FNewValue { get; set; }
    public long FOperatorId { get; set; }
    public string FRemark { get; set; } = string.Empty;
    public DateTime FCreateTime { get; set; } = DateTime.Now;

    // 导航属性
    public virtual TmTask? Task { get; set; }
}
