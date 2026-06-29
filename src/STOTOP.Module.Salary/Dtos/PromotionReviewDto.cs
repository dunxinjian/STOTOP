namespace STOTOP.Module.Salary.Dtos;

public class PromotionReviewDto
{
    public long Id { get; set; }
    public long OrgId { get; set; }
    public long EmployeeId { get; set; }
    public long RuleId { get; set; }
    public string? RuleName { get; set; }
    public long CurrentGradeId { get; set; }
    public string? CurrentGradeName { get; set; }
    public long TargetGradeId { get; set; }
    public string? TargetGradeName { get; set; }
    public DateTime TriggerTime { get; set; }
    public int AScoreSnapshot { get; set; }
    public int Status { get; set; }
    public long? ReviewerId { get; set; }
    public DateTime? ReviewTime { get; set; }
    public string? ReviewComment { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public DateTime CreateTime { get; set; }
}

public class CreatePromotionReviewRequest
{
    public long EmployeeId { get; set; }
    public long RuleId { get; set; }
    public long CurrentGradeId { get; set; }
    public long TargetGradeId { get; set; }
    public int AScoreSnapshot { get; set; }
}

public class ReviewPromotionRequest
{
    public bool IsApproved { get; set; }
    public string? Comment { get; set; }
    public DateTime? EffectiveDate { get; set; }
}
