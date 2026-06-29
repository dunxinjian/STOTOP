using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>插件定义（从 CfAgentDefinition 迁移而来）</summary>
public class CfPluginDef : BaseEntity, IOrgScoped
{
    /// <summary>组织ID</summary>
    public long FOrgId { get; set; }
    /// <summary>插件显示名称</summary>
    public string F插件名称 { get; set; } = string.Empty;
    /// <summary>插件类型（Input/Processing）</summary>
    public string F插件类型 { get; set; } = string.Empty;
    /// <summary>插件实现类全限定名</summary>
    public string F插件实现类型 { get; set; } = string.Empty;
    /// <summary>在插件定义中的排序号</summary>
    public int F排序号 { get; set; }
    /// <summary>状态（1=启用，0=禁用）</summary>
    public int F状态 { get; set; } = 1;
    /// <summary>输入来源类型</summary>
    public string? F输入来源类型 { get; set; }
    /// <summary>输入目标类型</summary>
    public string? F输入目标类型 { get; set; }
    /// <summary>目标表名</summary>
    public string? F目标表名 { get; set; }
    /// <summary>来源表名</summary>
    public string? F来源表名 { get; set; }
    /// <summary>配置JSON</summary>
    public string? F配置JSON { get; set; }
    /// <summary>是否支持回撤</summary>
    public bool F支持回撤 { get; set; } = true;
    /// <summary>创建时间</summary>
    public DateTime F创建时间 { get; set; } = DateTime.Now;
    /// <summary>更新时间</summary>
    public DateTime? F更新时间 { get; set; }
    /// <summary>关联规则ID</summary>
    public long? F规则ID { get; set; }

    // 导航属性
    public CfPluginRule? Rule { get; set; }
}
