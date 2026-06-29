using STOTOP.Core.Models;

namespace STOTOP.Module.CardFlow.Entities;

public class CfStageDefinition : BaseEntity
{
    public long FFlowVersionId { get; set; }
    public string FStageKey { get; set; } = string.Empty;
    public int FSortOrder { get; set; }
    public string FStageName { get; set; } = string.Empty;
    /// <summary>节点类型：human（人工）/ auto（自动）。注：原 batchAuto 已废弃，统一为 auto + F处理粒度=batch</summary>
    public string FType { get; set; } = "human";
    /// <summary>
    /// 处理粒度：card=卡片级（默认），batch=批次级
    /// </summary>
    public string F处理粒度 { get; set; } = "card";
    public string FApprovalMode { get; set; } = "single";
    public string? FAssigneeStrategy { get; set; }
    public string? FAssigneeConfigJson { get; set; }
    public string? FConditionJson { get; set; }
    public string? FInputFieldsJson { get; set; }

    // ── 旧版自动节点字段（已废弃，由 F插件注册ID + F插件规则ID 替代） ──
    // 保留属性以维持过渡期内服务层兼容编译，待 Task #3 插件适配完成后整体下线。
    [Obsolete("已废弃：请改用 F插件注册ID。本字段在插件适配（Task #3）完成后移除。")]
    public string? FAutoPluginName { get; set; }
    [Obsolete("已废弃：请改用 F插件规则ID 引用具体规则配置。本字段在插件适配（Task #3）完成后移除。")]
    public string? FAutoPluginConfigJson { get; set; }

    /// <summary>插件注册ID（外键 → CF自动插件注册.FID）</summary>
    public long? F插件注册ID { get; set; }

    /// <summary>插件规则ID（外键 → CF自动插件_规则.FID）</summary>
    public long? F插件规则ID { get; set; }

    public string? FFailurePolicyJson { get; set; }
    public string? FCcConfigJson { get; set; }
    public int? FTimeoutHours { get; set; }
    public int? FPriorityTemplate { get; set; }
}
