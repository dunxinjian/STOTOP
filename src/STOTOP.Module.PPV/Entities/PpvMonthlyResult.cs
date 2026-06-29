using STOTOP.Core.Models;

namespace STOTOP.Module.PPV.Entities;

/// <summary>
/// PPV 月度汇总（员工 + 期间 唯一）
/// </summary>
public class PpvMonthlyResult : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long F员工ID { get; set; }
    /// <summary>期间，格式 yyyyMM</summary>
    public string F期间 { get; set; } = string.Empty;
    public decimal F总产值 { get; set; }
    public decimal F本岗产值 { get; set; }
    public decimal F跨岗产值 { get; set; }
    /// <summary>综合质量等级：1=A 2=B 3=C 4=D</summary>
    public int F综合质量等级 { get; set; }
    public bool F是否跨岗清零 { get; set; }
    public string? F清零原因 { get; set; }
    public int FB分变化 { get; set; }
    public int FA分变化 { get; set; }
    public long F岗位ID快照 { get; set; }
    public long F部门ID快照 { get; set; }
    /// <summary>状态：1=正常 2=清零 3=异常</summary>
    public int F状态 { get; set; }
    public DateTime F创建时间 { get; set; } = DateTime.Now;
}
