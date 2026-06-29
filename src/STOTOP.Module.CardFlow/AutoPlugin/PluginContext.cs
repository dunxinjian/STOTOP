using System;
using System.Threading;

namespace STOTOP.Module.CardFlow.AutoPlugin;

/// <summary>
/// 插件执行上下文（轻量级，仅传递标识，插件通过 DI 自取数据）
/// </summary>
public class PluginContext
{
    /// <summary>批次ID（始终有值）</summary>
    public long BatchId { get; set; }
    
    /// <summary>卡片ID（卡片级执行时有值）</summary>
    public long? CardId { get; set; }
    
    /// <summary>节点定义ID</summary>
    public long StageDefinitionId { get; set; }
    
    /// <summary>节点引用的插件规则ID</summary>
    public long? PluginRuleId { get; set; }

    /// <summary>节点的 FAutoPlugin配置JSON（传递给兼容层 AgentContext）</summary>
    public string? ConfigJson { get; set; }

    /// <summary>批次所属组织ID（传递给兼容层 AgentContext）</summary>
    public long OrgId { get; set; }

    /// <summary>DI容器，插件自行获取 DbContext 等服务</summary>
    public IServiceProvider Services { get; set; } = null!;
    
    public CancellationToken CancellationToken { get; set; }
}
