using STOTOP.Core.Models;

namespace STOTOP.Module.Quality.Entities;

/// <summary>
/// 预警配置
/// </summary>
public class QlAlertConfig : BaseEntity, IOrgScoped
{
    public string F配置名称 { get; set; } = string.Empty;
    public string F阈值类型 { get; set; } = string.Empty;
    public decimal F阈值 { get; set; }
    public string F通知方式 { get; set; } = string.Empty;
    public string? F通知对象 { get; set; }
    public int F状态 { get; set; } = 1;
    public long FOrgId { get; set; }
    public DateTime F创建时间 { get; set; }
}
