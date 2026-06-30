using STOTOP.Core.Models;

namespace STOTOP.Module.Salary.Entities;

/// <summary>
/// 晋升历史
/// </summary>
public class PromotionHistory : BaseEntity, IOrgScoped, ITenantScoped
{
    public long FOrgId { get; set; }
    public long FTenantId { get; set; }  // 租户ID（区域公司，多租户隔离键）
    public long F员工ID { get; set; }
    public long F评审ID { get; set; }
    public long F原档位ID { get; set; }
    public long F新档位ID { get; set; }
    public decimal F原基本工资 { get; set; }
    public decimal F新基本工资 { get; set; }
    public DateTime F生效日期 { get; set; }
    public DateTime F创建时间 { get; set; } = DateTime.Now;
}
