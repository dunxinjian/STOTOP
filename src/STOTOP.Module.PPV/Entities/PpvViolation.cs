using STOTOP.Core.Models;

namespace STOTOP.Module.PPV.Entities;

/// <summary>
/// PPV 违规记录（按组织 + 员工 + 期间 + 违规类型 + 关联单据 唯一）
/// </summary>
public class PpvViolation : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long F员工ID { get; set; }
    /// <summary>期间，格式 yyyyMM</summary>
    public string F期间 { get; set; } = string.Empty;
    /// <summary>违规类型：1=质量违规 2=客诉 3=其他</summary>
    public int F违规类型 { get; set; }
    public string F关联单据ID { get; set; } = string.Empty;
    public decimal F清零金额 { get; set; }
    /// <summary>处理状态：0=待确认 1=已确认 2=已申诉</summary>
    public int F处理状态 { get; set; }
    public string? F备注 { get; set; }
    public DateTime F创建时间 { get; set; } = DateTime.Now;
}
