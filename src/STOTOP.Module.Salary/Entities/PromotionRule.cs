using STOTOP.Core.Models;

namespace STOTOP.Module.Salary.Entities;

/// <summary>
/// 晋升规则
/// </summary>
public class PromotionRule : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public string F规则名称 { get; set; } = string.Empty;
    public long F当前档位ID { get; set; }
    public long F目标档位ID { get; set; }
    /// <summary>A分累计达到此值触发</summary>
    public int FA分阈值 { get; set; }
    /// <summary>附加条件 JSON</summary>
    public string? F附加条件JSON { get; set; }
    public bool F启用状态 { get; set; } = true;
    public DateTime F创建时间 { get; set; } = DateTime.Now;
    public DateTime F更新时间 { get; set; } = DateTime.Now;
}
