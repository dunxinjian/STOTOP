using STOTOP.Core.Models;

namespace STOTOP.Module.KSF.Entities;

/// <summary>
/// KSF 岗位方案
/// </summary>
public class KsfPlan : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public string F名称 { get; set; } = string.Empty;
    /// <summary>关联 SysPosition.FID</summary>
    public long F岗位ID { get; set; }
    public DateTime F生效起期 { get; set; }
    public DateTime? F生效止期 { get; set; }
    public bool F启用状态 { get; set; } = true;
    /// <summary>运行模式：0=试运行 1=正式</summary>
    public int F运行模式 { get; set; }
    /// <summary>门槛规则 JSON：红线门槛 + B 分余额门槛</summary>
    public string? F门槛规则JSON { get; set; }
    /// <summary>岗位月加薪基数</summary>
    public decimal F岗位月加薪基数 { get; set; }
    public long F负责人ID { get; set; }
    public DateTime F创建时间 { get; set; } = DateTime.Now;
    public DateTime F更新时间 { get; set; } = DateTime.Now;
}
