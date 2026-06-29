using STOTOP.Core.Models;

namespace STOTOP.Module.PPV.Entities;

/// <summary>
/// PPV 产值记录（员工每条产值明细）
/// </summary>
public class PpvRecord : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long F员工ID { get; set; }
    /// <summary>期间，格式 yyyyMM</summary>
    public string F期间 { get; set; } = string.Empty;
    public long F模板ID { get; set; }
    public string F产值项编码 { get; set; } = string.Empty;
    public decimal F数量 { get; set; }
    public decimal F产值金额 { get; set; }
    /// <summary>质量等级：1=A 2=B 3=C 4=D</summary>
    public int F质量等级 { get; set; }
    public bool F是否跨岗 { get; set; }
    /// <summary>审核状态：0=待审核 1=已通过 2=已驳回</summary>
    public int F审核状态 { get; set; }
    public long? F审核人ID { get; set; }
    public DateTime? F审核时间 { get; set; }
    public string? F审核备注 { get; set; }
    public DateTime F创建时间 { get; set; } = DateTime.Now;
}
