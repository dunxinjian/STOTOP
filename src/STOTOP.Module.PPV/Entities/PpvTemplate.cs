using STOTOP.Core.Models;

namespace STOTOP.Module.PPV.Entities;

/// <summary>
/// PPV 产值模板（按组织 + 岗位 + 产值项编码 唯一）
/// </summary>
public class PpvTemplate : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    public string F名称 { get; set; } = string.Empty;
    public long F岗位ID { get; set; }
    public string F产值项编码 { get; set; } = string.Empty;
    public string F产值项名称 { get; set; } = string.Empty;
    public decimal F单价 { get; set; }
    public string F计量单位 { get; set; } = string.Empty;
    public bool F启用状态 { get; set; }
    public DateTime? F生效起期 { get; set; }
    public DateTime? F生效止期 { get; set; }
    public DateTime F创建时间 { get; set; } = DateTime.Now;
    public DateTime F更新时间 { get; set; } = DateTime.Now;
}
