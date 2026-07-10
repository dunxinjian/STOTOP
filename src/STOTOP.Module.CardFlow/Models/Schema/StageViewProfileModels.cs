namespace STOTOP.Module.CardFlow.Models.Schema;

public sealed class StageViewProfile
{
    public string? ProfileName { get; set; }
    public List<StageViewSection> Sections { get; set; } = new();
    public List<StageComponentSection> ComponentSections { get; set; } = new();
    public List<StageComponentRef> Components { get; set; } = new();
    public Dictionary<string, StageFieldAccessRule> FieldAccess { get; set; } = new();
    public Dictionary<string, StageDetailAccessRule> DetailAccess { get; set; } = new();
    public Dictionary<string, StageComponentAccessRule> ComponentAccess { get; set; } = new();
    public List<string> Actions { get; set; } = new();
    public StageSummaryProfile? Summary { get; set; }
}

public sealed class StageComponentSection
{
    public string Key { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string Region { get; set; } = "main";
    public List<string> ComponentIds { get; set; } = new();
}

public sealed class StageViewSection
{
    public string Key { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string Type { get; set; } = "fields";
    public List<StageViewFieldRef> Fields { get; set; } = new();
}

public sealed class StageViewFieldRef
{
    public string FieldKey { get; set; } = string.Empty;
    public string? Label { get; set; }
}

public sealed class StageFieldAccessRule
{
    public string Access { get; set; } = "readonly";
    public bool? Required { get; set; }
    public string? MaskPattern { get; set; }
    public object? DefaultValue { get; set; }
}

public sealed class StageDetailAccessRule
{
    public string Access { get; set; } = "readonly";
    public bool? Required { get; set; }
    public string? MaskPattern { get; set; }
    public object? DefaultValue { get; set; }
}

public sealed class StageActionPolicy
{
    public List<string> AllowedActions { get; set; } = new();
    /// <summary>需填写处理意见的动作（approve/reject/returnToStage/transfer），空=不强制。</summary>
    public List<string> OpinionRequiredActions { get; set; } = new();
    /// <summary>设计器自定义动作按钮（M8-C）：审批面板动态渲染，引擎按 Handler 分派执行。</summary>
    public List<CustomActionDefinition> CustomActions { get; set; } = new();
}

/// <summary>
/// 自定义动作定义（M8-C）。存储于 StageConfigEnvelope.ActionPolicy.CustomActions，无独立列。
/// Handler 初期仅支持 autoApprove（自动通过当前节点）/ autoReject（自动驳回）/ notify（触发通知/抄送）；
/// webhook 暂不支持（外部 URL 调用系 SSRF 面，设计器置灰禁用，留待后续评估）。
/// </summary>
public sealed class CustomActionDefinition
{
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Handler { get; set; } = string.Empty;
    public string? HandlerConfigJson { get; set; }
    public bool RequireOpinion { get; set; }
}

public sealed class StageSummaryProfile
{
    public List<string> Fields { get; set; } = new();
}
