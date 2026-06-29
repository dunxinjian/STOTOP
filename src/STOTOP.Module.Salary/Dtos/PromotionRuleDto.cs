namespace STOTOP.Module.Salary.Dtos;

public class PromotionRuleDto
{
    public long Id { get; set; }
    public long OrgId { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public long CurrentGradeId { get; set; }
    public string? CurrentGradeName { get; set; }
    public long TargetGradeId { get; set; }
    public string? TargetGradeName { get; set; }
    public int AScoreThreshold { get; set; }
    public string? ExtraConditionJson { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
}

public class CreatePromotionRuleRequest
{
    public string RuleName { get; set; } = string.Empty;
    public long CurrentGradeId { get; set; }
    public long TargetGradeId { get; set; }
    public int AScoreThreshold { get; set; }
    public string? ExtraConditionJson { get; set; }
    public bool IsEnabled { get; set; } = true;
}

public class UpdatePromotionRuleRequest
{
    public string RuleName { get; set; } = string.Empty;
    public long CurrentGradeId { get; set; }
    public long TargetGradeId { get; set; }
    public int AScoreThreshold { get; set; }
    public string? ExtraConditionJson { get; set; }
    public bool IsEnabled { get; set; } = true;
}
