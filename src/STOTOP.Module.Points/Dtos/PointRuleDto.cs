using STOTOP.Core.Models;

namespace STOTOP.Module.Points.Dtos;

/// <summary>
/// 积分规则 - 列表 DTO
/// </summary>
public class PointRuleListDto
{
    public long Id { get; set; }
    public long OrgId { get; set; }
    public long SourceId { get; set; }
    public string? SourceName { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public string RuleCode { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public int PointValue { get; set; }
    public string? ConditionDescription { get; set; }
    public int CycleLimit { get; set; }
    public bool RequireApproval { get; set; }
    public int SortOrder { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
}

/// <summary>
/// 积分规则 - 详情 DTO
/// </summary>
public class PointRuleDetailDto
{
    public long Id { get; set; }
    public long OrgId { get; set; }
    public long SourceId { get; set; }
    public string? SourceName { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public string RuleCode { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public int PointValue { get; set; }
    public string? ConditionExpression { get; set; }
    public string? ConditionDescription { get; set; }
    public string? MultiplierRule { get; set; }
    public int CycleLimit { get; set; }
    public bool RequireApproval { get; set; }
    public int SortOrder { get; set; }
    public bool IsEnabled { get; set; }
    /// <summary>账户类型（1=A / 2=B）</summary>
    public int AccountType { get; set; } = 2;
    /// <summary>清算策略（0=不清算 / 1=月清 / 2=年清）</summary>
    public int ResetStrategy { get; set; }
    /// <summary>转换比例</summary>
    public decimal ConvertRatio { get; set; } = 1.0m;
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
}

/// <summary>
/// 创建积分规则请求
/// </summary>
public class CreatePointRuleRequest
{
    public long SourceId { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public string RuleCode { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public int PointValue { get; set; }
    public string? ConditionExpression { get; set; }
    public string? ConditionDescription { get; set; }
    public string? MultiplierRule { get; set; }
    public int CycleLimit { get; set; }
    public bool RequireApproval { get; set; }
    public int SortOrder { get; set; }
    /// <summary>账户类型（1=A / 2=B）默认 B</summary>
    public int AccountType { get; set; } = 2;
    /// <summary>清算策略（0=不清算 / 1=月清 / 2=年清）</summary>
    public int ResetStrategy { get; set; }
    /// <summary>转换比例</summary>
    public decimal ConvertRatio { get; set; } = 1.0m;
}

/// <summary>
/// 更新积分规则请求
/// </summary>
public class UpdatePointRuleRequest
{
    public long SourceId { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public int PointValue { get; set; }
    public string? ConditionExpression { get; set; }
    public string? ConditionDescription { get; set; }
    public string? MultiplierRule { get; set; }
    public int CycleLimit { get; set; }
    public bool RequireApproval { get; set; }
    public int SortOrder { get; set; }
    public bool IsEnabled { get; set; }
    /// <summary>账户类型（1=A / 2=B）</summary>
    public int AccountType { get; set; } = 2;
    /// <summary>清算策略（0=不清算 / 1=月清 / 2=年清）</summary>
    public int ResetStrategy { get; set; }
    /// <summary>转换比例</summary>
    public decimal ConvertRatio { get; set; } = 1.0m;
}

/// <summary>
/// 积分规则查询请求（支持按来源筛选）
/// </summary>
public class PointRulePagedRequest : PagedRequest
{
    public long? SourceId { get; set; }
    public bool? IsEnabled { get; set; }
}
