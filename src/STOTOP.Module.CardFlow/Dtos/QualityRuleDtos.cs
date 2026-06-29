namespace STOTOP.Module.CardFlow.Dtos;

public class QualityRuleListRequest
{
    public string? TargetTable { get; set; }
    public long? OrgId { get; set; }
    public bool? IsEnabled { get; set; }
}

public class QualityRuleDto
{
    public long Id { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public string RuleCode { get; set; } = string.Empty;
    public string? TargetTable { get; set; }
    public string RuleLevel { get; set; } = string.Empty;
    public string CheckType { get; set; } = string.Empty;
    public string? TargetField { get; set; }
    public string? ParametersJson { get; set; }
    public string ErrorTypeCode { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string? QualityDimension { get; set; }
    public string? MessageTemplate { get; set; }
    public string? SuggestedFix { get; set; }
    public bool IsBlocking { get; set; }
    public long OrgId { get; set; }
    public int Sort { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
}

public class CreateQualityRuleRequest
{
    public string RuleName { get; set; } = string.Empty;
    public string RuleCode { get; set; } = string.Empty;
    public string? TargetTable { get; set; }
    public string RuleLevel { get; set; } = "Field";
    public string CheckType { get; set; } = "NotNull";
    public string? TargetField { get; set; }
    public string? ParametersJson { get; set; }
    public string ErrorTypeCode { get; set; } = string.Empty;
    public string Severity { get; set; } = "Warning";
    public string? QualityDimension { get; set; }
    public string? MessageTemplate { get; set; }
    public string? SuggestedFix { get; set; }
    public bool IsBlocking { get; set; }
    public long OrgId { get; set; }
    public int Sort { get; set; }
    public bool IsEnabled { get; set; } = true;
}

public class UpdateQualityRuleRequest : CreateQualityRuleRequest { }

public class TestQualityRuleRequest
{
    public long? RuleId { get; set; }
    public CreateQualityRuleRequest? RuleDefinition { get; set; }
    public long BatchId { get; set; }
    public int MaxRows { get; set; } = 100;
}

public class TestQualityRuleResult
{
    public int TotalChecked { get; set; }
    public int ViolationCount { get; set; }
    public List<TestViolationItem> Violations { get; set; } = new();
}

public class TestViolationItem
{
    public int RowIndex { get; set; }
    public string? FieldName { get; set; }
    public string? OriginalValue { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}
