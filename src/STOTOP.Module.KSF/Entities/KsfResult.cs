using STOTOP.Core.Models;

namespace STOTOP.Module.KSF.Entities;

/// <summary>
/// KSF 结果（员工 + 期间 唯一）
/// </summary>
public class KsfResult : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public long F员工ID { get; set; }
    /// <summary>期间，格式 yyyyMM</summary>
    public string F期间 { get; set; } = string.Empty;
    public long F方案ID { get; set; }
    public long F岗位ID快照 { get; set; }
    public long F部门ID快照 { get; set; }
    public long? F经营单元ID快照 { get; set; }
    public decimal F固定部分 { get; set; }
    public decimal F浮动部分 { get; set; }
    public decimal F加薪 { get; set; }
    public decimal F扣减 { get; set; }
    public decimal F实发 { get; set; }
    /// <summary>方案快照 JSON（生成时存档）</summary>
    public string? F方案快照JSON { get; set; }
    /// <summary>状态：1=试运行 2=正式 3=取数异常</summary>
    public int F状态 { get; set; }
    public DateTime F创建时间 { get; set; } = DateTime.Now;
}
