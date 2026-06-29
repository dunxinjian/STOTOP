namespace STOTOP.Module.CardFlow.Dtos;

// 审批请求
public class ApproveRequest
{
    public string? Opinion { get; set; }
    public Dictionary<string, object>? SupplementData { get; set; }
    public List<DetailRowEditRequest>? DetailEdits { get; set; }
    public string? ConcurrencyStamp { get; set; }
}

public sealed class DetailRowEditRequest
{
    public long? DetailId { get; set; }
    public string DetailTableKey { get; set; } = "default";
    public int RowIndex { get; set; }
    public Dictionary<string, object> Values { get; set; } = new();
}

public class RejectRequest
{
    public string? Opinion { get; set; }
    public long? TargetStageId { get; set; }
    public string? ReturnMode { get; set; }
    public string? ConcurrencyStamp { get; set; }
}

public class CountersignRequest
{
    public long UserId { get; set; }
    public string InsertMode { get; set; } = "after";
    public string? Opinion { get; set; }
}

public class TransferRequest
{
    public long NewUserId { get; set; }
    public string? Opinion { get; set; }
}

public class CcRequest
{
    public List<long> UserIds { get; set; } = new();
    public string? Opinion { get; set; }
}

public class VoidRequest
{
    public string? Opinion { get; set; }
}

public class UrgeRequest
{
    public string? Message { get; set; }
}

// 流程定义请求
public class FlowDefinitionQueryRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Status { get; set; }
    public long? OrgId { get; set; }
    public string? Keyword { get; set; }
}

public class CreateFlowDefinitionRequest
{
    public string FlowName { get; set; } = string.Empty;
    public string FlowCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? NumberTemplate { get; set; }
    public string? TitleTemplate { get; set; }
    public string? AllowedRolesJson { get; set; }
    public long? FlowGroupId { get; set; }
    public long OrgId { get; set; }
}

public class UpdateFlowDefinitionRequest
{
    public string? FlowName { get; set; }
    public string? Description { get; set; }
    public string? NumberTemplate { get; set; }
    public string? TitleTemplate { get; set; }
    public string? AllowedRolesJson { get; set; }
    public long? FlowGroupId { get; set; }
}

public class SaveDraftVersionRequest
{
    public string? CardSchemaJson { get; set; }
    public string? DetailSchemaJson { get; set; }
    public string? FlowSettingsJson { get; set; }
    public List<StageDefinitionRequest> Stages { get; set; } = new();
    public List<StageRouteRuleRequest> Routes { get; set; } = new();
    public List<DynamicStagePolicyRequest> DynamicPolicies { get; set; } = new();
}

public class StageDefinitionRequest
{
    public string? StageKey { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "human";
    public int SortOrder { get; set; }
    public string? ApprovalMode { get; set; }
    public string? AssigneeStrategy { get; set; }
    public string? AssigneeConfigJson { get; set; }
    public string? ConditionJson { get; set; }
    public string? InputFieldsJson { get; set; }

    /// <summary>处理粒度：card=卡片级（默认），batch=批次级</summary>
    public string ProcessingGranularity { get; set; } = "card";

    /// <summary>插件注册ID（外键 → CfAutoPluginRegistry.FID）</summary>
    public long? PluginRegistryId { get; set; }

    /// <summary>插件规则ID（外键 → CfPluginRule.FID）</summary>
    public long? PluginRuleId { get; set; }

    public string? FailurePolicyJson { get; set; }
    public string? CcConfigJson { get; set; }
    public int? TimeoutHours { get; set; }
    public int? PriorityTemplate { get; set; }
}

public class StageRouteRuleRequest
{
    public string EdgeKey { get; set; } = string.Empty;
    public string FromStageKey { get; set; } = string.Empty;
    public string ToStageKey { get; set; } = string.Empty;
    public string RouteName { get; set; } = string.Empty;
    public string? ConditionJson { get; set; }
    public int Priority { get; set; }
    public bool IsDefault { get; set; }
    public string? Status { get; set; }
    public string? FailurePolicyJson { get; set; }
}

public class DynamicStagePolicyRequest
{
    public string PolicyKey { get; set; } = string.Empty;
    public string SourceStageKey { get; set; } = string.Empty;
    public string PolicyName { get; set; } = string.Empty;
    public string StrategyType { get; set; } = string.Empty;
    public string? StrategyConfigJson { get; set; }
    public string? ConditionJson { get; set; }
    public string? TriggerTiming { get; set; }
    public string? InsertPosition { get; set; }
    public string? ContinuationStageKey { get; set; }
    public int Priority { get; set; }
    public int? MaxInsertCount { get; set; }
    public string? FallbackJson { get; set; }
    public string? Status { get; set; }
}

public class CardFlowPathPreviewRequest
{
    public long? FlowVersionId { get; set; }
    public string? DataJson { get; set; }
    public string? InitialDataJson { get; set; }
    public long? InitiatorId { get; set; }
    public long? OrgId { get; set; }
    public string? SourceModule { get; set; }
    public string? SourceType { get; set; }
    public long? SourceId { get; set; }
    public int? MaxSteps { get; set; }
}

// 卡片请求
public class CardQueryRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Status { get; set; }
    public long? FlowId { get; set; }
    public long? OrgId { get; set; }
    public long? InitiatorId { get; set; }
    public string? SourceModule { get; set; }
    public string? SourceType { get; set; }
    public long? SourceId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class CreateCardRequest
{
    public long FlowDefinitionId { get; set; }
    public string? DataJson { get; set; }
    public long OrgId { get; set; }
    public string? SourceModule { get; set; }
    public string? SourceType { get; set; }
    public long? SourceId { get; set; }
    public string? ReturnUrl { get; set; }
    public string? InitialDataJson { get; set; }
    public string? SourceTitle { get; set; }
}

public class UpdateCardRequest
{
    public string? DataJson { get; set; }
    public string? ConcurrencyStamp { get; set; }
    public List<UpdateCardDetailRequest>? Details { get; set; }
}

public class UpdateCardDetailRequest
{
    public long? Id { get; set; }
    public string DetailTableKey { get; set; } = "default";
    public int SortOrder { get; set; }
    public string? DataJson { get; set; }
}

public class CreateRelationRequest
{
    public long TargetCardId { get; set; }
    public string RelationType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal? OffsetAmount { get; set; }
}

// 待办请求
public class TodoQueryRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Status { get; set; }
    public long? FlowId { get; set; }
}

// 待办统计请求
public class TodoStatsRequest
{
    public long OrgId { get; set; }
    public long? FlowId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    /// <summary>超过该小时数视为超时（默认24小时）</summary>
    public int TimeoutHours { get; set; } = 24;
}

// 审计日志查询请求
public class AuditLogQueryRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    /// <summary>以逗号分隔的操作类型列表，例：submit,approve</summary>
    public string? ActionTypes { get; set; }
    public string? OperatorName { get; set; }
    public string? CardNumber { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

// 委托请求
public class CreateDelegationRequest
{
    public long TrusteeId { get; set; }
    public string TrusteeName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string? ApplicableFlowsJson { get; set; }
}

public class UpdateDelegationRequest
{
    public long? TrusteeId { get; set; }
    public string? TrusteeName { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string? ApplicableFlowsJson { get; set; }
}

// 流程组请求
public class CreateFlowGroupRequest
{
    public string GroupName { get; set; } = string.Empty;
    public string GroupCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public long OrgId { get; set; }
}

public class UpdateFlowGroupRequest
{
    public string? GroupName { get; set; }
    public string? Description { get; set; }
    public string? Status { get; set; }
}

public class SaveFlowGroupLinkRequest
{
    public long SourceFlowId { get; set; }
    public long TargetFlowId { get; set; }
    public string? TriggerCondition { get; set; }
    public string? FieldMappingJson { get; set; }
    public string TriggerMode { get; set; } = "auto";
    public int SortOrder { get; set; }
}

// 通知渠道配置请求
public class SaveNotificationSettingsRequest
{
    public string? DingtalkAppKey { get; set; }
    public string? DingtalkAppSecret { get; set; }
    public string? DingtalkAgentId { get; set; }
    public bool? DingtalkEnabled { get; set; }
    public string? DetailUrlTemplate { get; set; }
}

public class TestNotificationRequest
{
    public string Channel { get; set; } = "dingtalk";
    public string? AppKey { get; set; }
    public string? AppSecret { get; set; }
    public string? AgentId { get; set; }
}

// 克隆流程定义请求
public class CloneFlowDefinitionRequest
{
    public string FlowName { get; set; } = string.Empty;
    public string FlowCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public long? OrgId { get; set; }
}
