using STOTOP.Core.Models;

namespace STOTOP.Module.Quality.Dtos;

/// <summary>
/// 规则列表项（用于列表展示）
/// </summary>
public class QualityRuleDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string BusinessLine { get; set; } = string.Empty;
    public string? ConditionExpression { get; set; }
    public int DispatchMethod { get; set; }
    public string? DispatchTarget { get; set; }
    public int DefaultPriority { get; set; }
    public int TimeoutHours { get; set; }
    public int Status { get; set; }
    public string? Description { get; set; }
    public int ConditionCount { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime? UpdatedTime { get; set; }
}

/// <summary>
/// 规则详情（含条件列表）
/// </summary>
public class QualityRuleDetailDto : QualityRuleDto
{
    public List<RuleConditionDto> Conditions { get; set; } = new();
}

/// <summary>
/// 规则条件DTO
/// </summary>
public class RuleConditionDto
{
    public long? Id { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public string Threshold { get; set; } = string.Empty;
    public string LogicRelation { get; set; } = "AND";
    public int Sort { get; set; }
}

/// <summary>
/// 创建规则请求
/// </summary>
public class CreateRuleRequest
{
    public string Name { get; set; } = string.Empty;
    public string BusinessLine { get; set; } = string.Empty;
    public int DispatchMethod { get; set; }
    public string? DispatchTarget { get; set; }
    public int DefaultPriority { get; set; }
    public int TimeoutHours { get; set; } = 24;
    public string? Description { get; set; }
    public List<RuleConditionDto> Conditions { get; set; } = new();
}

/// <summary>
/// 更新规则请求（复用 CreateRuleRequest）
/// </summary>
public class UpdateRuleRequest : CreateRuleRequest { }

/// <summary>
/// 规则分页请求
/// </summary>
public class RulePagedRequest : PagedRequest
{
    public string? BusinessLine { get; set; }
    public int? Status { get; set; }
}
