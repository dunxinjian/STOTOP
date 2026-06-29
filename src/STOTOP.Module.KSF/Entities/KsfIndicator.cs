using STOTOP.Core.Models;

namespace STOTOP.Module.KSF.Entities;

/// <summary>
/// KSF 指标定义
/// </summary>
public class KsfIndicator : BaseEntity, IOrgScoped
{
    public long FOrgId { get; set; }
    /// <summary>指标编码（组织内唯一）</summary>
    public string F编码 { get; set; } = string.Empty;
    /// <summary>指标名称</summary>
    public string F名称 { get; set; } = string.Empty;
    /// <summary>计量单位：元、件、% 等</summary>
    public string F计量单位 { get; set; } = string.Empty;
    /// <summary>取数类型：1=SQL模板 2=Agent 3=常量值 4=外部API</summary>
    public int F取数类型 { get; set; }
    /// <summary>取数SQL（仅 F取数类型=1 使用）</summary>
    public string? F取数SQL { get; set; }
    /// <summary>取数Agent DI key（仅 F取数类型=2 使用）</summary>
    public string? F取数Agent { get; set; }
    /// <summary>取数补充参数 JSON（4 种取数方式共用）</summary>
    public string? F取数参数JSON { get; set; }
    /// <summary>方向：1=正向 2=逆向</summary>
    public int F方向 { get; set; } = 1;
    /// <summary>业务对象类型短编码（默认 KSF）</summary>
    public string F业务对象类型 { get; set; } = "KSF";
    public bool F是否启用 { get; set; } = true;
    public DateTime F创建时间 { get; set; } = DateTime.Now;
    public DateTime F更新时间 { get; set; } = DateTime.Now;
}
