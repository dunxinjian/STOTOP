using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

/// <summary>
/// CF自动插件注册（全局插件类型目录）
/// 存储所有已注册的自动插件类型元信息，作为流程节点选择插件类型的目录。
/// 与 CfAutoPluginRule（规则）共同支撑流程节点的插件配置。
/// </summary>
public class CfAutoPluginRegistry : BaseEntity
{
    /// <summary>插件唯一编码（如 AutoVoucher、ExcelInput），用于代码识别</summary>
    public string F插件编码 { get; set; } = string.Empty;

    /// <summary>插件显示名称</summary>
    public string F插件名称 { get; set; } = string.Empty;

    /// <summary>插件类型：Input（输入）/ Processing（处理）</summary>
    public string F插件类型 { get; set; } = string.Empty;

    /// <summary>处理粒度：card=卡片级 / batch=批次级</summary>
    public string F处理粒度 { get; set; } = string.Empty;

    /// <summary>插件默认配置（JSON）</summary>
    public string? F默认配置JSON { get; set; }

    /// <summary>功能描述</summary>
    public string? F说明 { get; set; }

    /// <summary>状态：1=启用，0=禁用</summary>
    public int F状态 { get; set; } = 1;
}
