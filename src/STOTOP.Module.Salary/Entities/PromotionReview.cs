using STOTOP.Core.Models;

namespace STOTOP.Module.Salary.Entities;

/// <summary>
/// 晋升评审
/// </summary>
public class PromotionReview : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long F员工ID { get; set; }
    public long F规则ID { get; set; }
    public long F当前档位ID { get; set; }
    public long F目标档位ID { get; set; }
    public DateTime F触发时间 { get; set; }
    /// <summary>触发时的 A 分余额</summary>
    public int FA分快照 { get; set; }
    /// <summary>状态：0=待评审 1=通过 2=驳回 3=撤回</summary>
    public int F状态 { get; set; }
    public long? F评审人ID { get; set; }
    public DateTime? F评审时间 { get; set; }
    public string? F评审意见 { get; set; }
    public DateTime? F生效日期 { get; set; }
    public DateTime F创建时间 { get; set; } = DateTime.Now;
}
