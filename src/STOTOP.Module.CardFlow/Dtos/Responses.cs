using STOTOP.Module.CardFlow.Models.Schema;

namespace STOTOP.Module.CardFlow.Dtos;

public class FlowDefinitionDto
{
    public long Id { get; set; }
    public string FlowName { get; set; } = string.Empty;
    public string FlowCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? NumberTemplate { get; set; }
    public string? TitleTemplate { get; set; }
    public string? AllowedRolesJson { get; set; }
    public long? FlowGroupId { get; set; }
    public long OrgId { get; set; }
    public DateTime CreatedTime { get; set; }
    public string? TriggerConfigJson { get; set; }
    public long? AccountSetId { get; set; }
    public int? CurrentVersion { get; set; }
    public DateTime? LastPublishedTime { get; set; }
    public bool HasDraft { get; set; }
    public int? DraftVersion { get; set; }
    public bool IsTemplate { get; set; }
}

public class FlowVersionDto
{
    public long Id { get; set; }
    public int VersionNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsCurrentVersion { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime? PublishTime { get; set; }
}

public class FlowVersionDetailDto : FlowVersionDto
{
    public string? CardSchemaJson { get; set; }
    public string? DetailSchemaJson { get; set; }
    public string? FlowSettingsJson { get; set; }
    public List<StageDefinitionDto> Stages { get; set; } = new();
    public List<StageRouteRuleDto> Routes { get; set; } = new();
    public List<DynamicStagePolicyDto> DynamicPolicies { get; set; } = new();
}

public class StageDefinitionDto
{
    public long Id { get; set; }
    public string StageKey { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public string StageName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? ApprovalMode { get; set; }
    public string? AssigneeStrategy { get; set; }
    public string? AssigneeConfigJson { get; set; }
    public string? ConditionJson { get; set; }
    public string? InputFieldsJson { get; set; }

    /// <summary>处理粒度：card=卡片级，batch=批次级</summary>
    public string ProcessingGranularity { get; set; } = "card";

    /// <summary>插件注册ID</summary>
    public long? PluginRegistryId { get; set; }

    /// <summary>插件规则ID</summary>
    public long? PluginRuleId { get; set; }

    public string? FailurePolicyJson { get; set; }
    public string? CcConfigJson { get; set; }
    public int? TimeoutHours { get; set; }
    public int? PriorityTemplate { get; set; }
}

public class StageRouteRuleDto
{
    public long Id { get; set; }
    public string EdgeKey { get; set; } = string.Empty;
    public string FromStageKey { get; set; } = string.Empty;
    public string ToStageKey { get; set; } = string.Empty;
    public string RouteName { get; set; } = string.Empty;
    public string? ConditionJson { get; set; }
    public int Priority { get; set; }
    public bool IsDefault { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? FailurePolicyJson { get; set; }
}

public class DynamicStagePolicyDto
{
    public long Id { get; set; }
    public string PolicyKey { get; set; } = string.Empty;
    public string SourceStageKey { get; set; } = string.Empty;
    public string PolicyName { get; set; } = string.Empty;
    public string StrategyType { get; set; } = string.Empty;
    public string? StrategyConfigJson { get; set; }
    public string? ConditionJson { get; set; }
    public string TriggerTiming { get; set; } = string.Empty;
    public string InsertPosition { get; set; } = string.Empty;
    public string? ContinuationStageKey { get; set; }
    public int Priority { get; set; }
    public int MaxInsertCount { get; set; }
    public string? FallbackJson { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class CardFlowPathPreviewDto
{
    public long FlowDefinitionId { get; set; }
    public long FlowVersionId { get; set; }
    public List<CardFlowPathPreviewStepDto> Steps { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class CardFlowPathPreviewStepDto
{
    public int Order { get; set; }
    public string StepType { get; set; } = "stage";
    public string StageKey { get; set; } = string.Empty;
    public string StageName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? SelectedEdgeKey { get; set; }
    public string? SelectedRouteName { get; set; }
    public string? Reason { get; set; }
    public string? PolicyKey { get; set; }
    public string? PolicyName { get; set; }
    public List<CardFlowPathPreviewCandidateDto> Candidates { get; set; } = new();
}

public class CardFlowPathPreviewCandidateDto
{
    public string EdgeKey { get; set; } = string.Empty;
    public string RouteName { get; set; } = string.Empty;
    public string ToStageKey { get; set; } = string.Empty;
    public int Priority { get; set; }
    public bool IsDefault { get; set; }
    public bool Matched { get; set; }
    public string Explanation { get; set; } = string.Empty;
    public List<string> TypeErrors { get; set; } = new();
}

public class AvailableFlowDto
{
    public long Id { get; set; }
    public string FlowName { get; set; } = string.Empty;
    public string FlowCode { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class CardListDto
{
    public long Id { get; set; }
    public string? CardNumber { get; set; }
    public string? Title { get; set; }
    public string Status { get; set; } = string.Empty;
    public string FlowName { get; set; } = string.Empty;
    public string InitiatorName { get; set; } = string.Empty;
    public DateTime CreatedTime { get; set; }
    public DateTime? SubmitTime { get; set; }
    public DateTime? CompletedTime { get; set; }
    public string? SourceModule { get; set; }
    public string? SourceType { get; set; }
    public long? SourceId { get; set; }
    public string? ReturnUrl { get; set; }
    public string? SourceTitle { get; set; }
}

public class CardDetailDto : CardListDto
{
    // 预填业务载荷仅在详情下发（经 CardRedactionService 脱敏）；列表不携带，避免无卡片级访问门的明文泄露。详见脱敏链重构 spec §9.1。
    public string? InitialDataJson { get; set; }
    public long FlowDefinitionId { get; set; }
    public long FlowVersionId { get; set; }
    public long InitiatorId { get; set; }
    public long? CurrentStageInstanceId { get; set; }
    public string? DataJson { get; set; }
    public int CurrentRound { get; set; }
    public string? ConcurrencyStamp { get; set; }
    public List<StageInstanceDto> StageInstances { get; set; } = new();
    public List<CardDetailRowDto> Details { get; set; } = new();
    public List<CardFlowRuntimeAuditDto> AuditTrail { get; set; } = new();
    public StageWorkViewDto? CurrentStageWorkView { get; set; }
}

public class StageInstanceDto
{
    public long Id { get; set; }
    public long? StageDefinitionId { get; set; }
    public string StageName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Round { get; set; }
    public string? FinalAction { get; set; }
    public string? Opinion { get; set; }
    public DateTime? ActivatedTime { get; set; }
    public DateTime? CompletedTime { get; set; }
    public bool IsTimeout { get; set; }
    public List<AssigneeDto> Assignees { get; set; } = new();
}

public class AssigneeDto
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Opinion { get; set; }
    public DateTime? CompletedTime { get; set; }
}

public class CardDetailRowDto
{
    public long Id { get; set; }
    public string DetailTableKey { get; set; } = "default";
    public int SortOrder { get; set; }
    public string? DataJson { get; set; }
}

public class StageWorkViewDto
{
    public List<StageViewSectionDto> Sections { get; set; } = new();
    public Dictionary<string, StageFieldAccessDto> FieldAccess { get; set; } = new();
    public Dictionary<string, StageDetailAccessDto> DetailAccess { get; set; } = new();
    public List<CardComponentRuntimeDto> Components { get; set; } = new();
    public Dictionary<string, object?> DetailSummary { get; set; } = new();
    public StageActionPolicyDto ActionPolicy { get; set; } = new();
    public StageSummaryProfileDto? Summary { get; set; }
}

public class StageViewSectionDto
{
    public string Key { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string Type { get; set; } = "fields";
    public List<StageViewFieldDto> Fields { get; set; } = new();
}

public class StageViewFieldDto
{
    public string FieldKey { get; set; } = string.Empty;
    public string? Label { get; set; }
}

public class StageFieldAccessDto
{
    public string Access { get; set; } = "readonly";
    public bool? Required { get; set; }
    public string? MaskPattern { get; set; }
}

public class StageDetailAccessDto
{
    public string Access { get; set; } = "readonly";
    public bool? Required { get; set; }
    public string? MaskPattern { get; set; }
}

public class StageActionPolicyDto
{
    public List<string> AllowedActions { get; set; } = new();
}

public class StageSummaryProfileDto
{
    public List<string> Fields { get; set; } = new();
}

public class TodoItemDto
{
    public long Id { get; set; }
    public long CardId { get; set; }
    public string? CardNumber { get; set; }
    public string? Title { get; set; }
    public string FlowName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Priority { get; set; }
    public string InitiatorName { get; set; } = string.Empty;
    public DateTime CreatedTime { get; set; }
}

public class TodoCountDto
{
    public int Todo { get; set; }
    public int Initiated { get; set; }
    public int Cc { get; set; }
}

public class TodoStatsDto
{
    public int TotalPending { get; set; }
    public double AvgProcessHours { get; set; }
    public double TimeoutRate { get; set; }
    public int TodayCompleted { get; set; }
    public List<FlowTodoStat> FlowStats { get; set; } = new();
    public List<TodoTrendPoint> Trend { get; set; } = new();
}

public class FlowTodoStat
{
    public string FlowName { get; set; } = string.Empty;
    public int PendingCount { get; set; }
    public int CompletedCount { get; set; }
    public double AvgProcessHours { get; set; }
    public double TimeoutRate { get; set; }
}

public class TodoTrendPoint
{
    public string Date { get; set; } = string.Empty;
    public double AvgProcessHours { get; set; }
}

public class ActionLogDto
{
    public long Id { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string OperatorName { get; set; } = string.Empty;
    public DateTime OperationTime { get; set; }
    public string? Opinion { get; set; }
    public string? DetailJson { get; set; }
}

public class AuditLogItemDto
{
    public long Id { get; set; }
    public long CardId { get; set; }
    public string? CardNumber { get; set; }
    public string? CardTitle { get; set; }
    public string FlowName { get; set; } = string.Empty;
    public string? StageName { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string OperatorName { get; set; } = string.Empty;
    public DateTime OperationTime { get; set; }
    public string? Opinion { get; set; }
    public string? DetailJson { get; set; }
}

public class DelegationDto
{
    public long Id { get; set; }
    public long DelegatorId { get; set; }
    public string DelegatorName { get; set; } = string.Empty;
    public long TrusteeId { get; set; }
    public string TrusteeName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string? ApplicableFlowsJson { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class FlowGroupDto
{
    public long Id { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string GroupCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;
    public int FlowCount { get; set; }
    public int LinkCount { get; set; }
}

public class FlowGroupLinkDto
{
    public long Id { get; set; }
    public long SourceFlowId { get; set; }
    public string SourceFlowName { get; set; } = string.Empty;
    public long TargetFlowId { get; set; }
    public string TargetFlowName { get; set; } = string.Empty;
    public string? TriggerCondition { get; set; }
    public string? FieldMappingJson { get; set; }
    public string TriggerMode { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public class CardBalanceDto
{
    public long Id { get; set; }
    public long CardId { get; set; }
    public string? CardNumber { get; set; }
    public string? CardTitle { get; set; }
    public decimal OriginalAmount { get; set; }
    public decimal OffsetAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class CardRelationDto
{
    public long Id { get; set; }
    public long SourceCardId { get; set; }
    public string? SourceCardNumber { get; set; }
    public long TargetCardId { get; set; }
    public string? TargetCardNumber { get; set; }
    public string RelationType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal? OffsetAmount { get; set; }
}

public class SchemaFieldDefinition
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool Required { get; set; }
    public bool Readonly { get; set; }
    public bool Sensitive { get; set; }
    public string? MaskPattern { get; set; }
}

public class NotificationSettingsDto
{
    public string? DingtalkAppKey { get; set; }
    public string? DingtalkAppSecret { get; set; }
    public string? DingtalkAgentId { get; set; }
    public bool DingtalkEnabled { get; set; }
    public string? DetailUrlTemplate { get; set; }
    public string? CallbackUrl { get; set; }
}

public class TestNotificationResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
}
