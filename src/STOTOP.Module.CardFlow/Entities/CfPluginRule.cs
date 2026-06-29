using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>插件规则（从 CfAutoPluginRule 迁移而来）</summary>
public class CfPluginRule : BaseEntity, IOrgScoped
{
    /// <summary>组织ID（所属组织，必须>0）</summary>
    public long FOrgId { get; set; }
    /// <summary>类型编码</summary>
    public string F类型编码 { get; set; } = string.Empty;
    /// <summary>规则名称</summary>
    public string F规则名称 { get; set; } = string.Empty;
    /// <summary>规则配置JSON</summary>
    public string? F规则配置JSON { get; set; }
    /// <summary>V1版本规则配置备份</summary>
    public string? F规则配置V1备份 { get; set; }
    /// <summary>状态（1=启用，0=禁用）</summary>
    public int F状态 { get; set; } = 1;
    /// <summary>说明</summary>
    public string? F说明 { get; set; }
    /// <summary>并发戳</summary>
    public string FConcurrencyStamp { get; set; } = Guid.NewGuid().ToString("N");
    /// <summary>创建时间</summary>
    public DateTime F创建时间 { get; set; } = DateTime.Now;
    /// <summary>更新时间</summary>
    public DateTime? F更新时间 { get; set; }
}
