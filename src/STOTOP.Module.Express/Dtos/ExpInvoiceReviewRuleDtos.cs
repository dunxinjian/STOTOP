namespace STOTOP.Module.Express.Dtos;

/// <summary>
/// 审核规则DTO
/// </summary>
public class ReviewRuleDto
{
    public long Id { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public int RuleType { get; set; }
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public decimal? Threshold { get; set; }
    public string? ClientId { get; set; }
    public string? BrandCode { get; set; }
    public int Priority { get; set; }
    public bool Enabled { get; set; }
    public DateTime CreatedTime { get; set; }
}

/// <summary>
/// 创建审核规则请求
/// </summary>
public class CreateReviewRuleRequest
{
    public string RuleName { get; set; } = string.Empty;
    public int RuleType { get; set; }
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public decimal? Threshold { get; set; }
    public string? ClientId { get; set; }
    public string? BrandCode { get; set; }
    public int Priority { get; set; }
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// 更新审核规则请求
/// </summary>
public class UpdateReviewRuleRequest
{
    public string RuleName { get; set; } = string.Empty;
    public int RuleType { get; set; }
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public decimal? Threshold { get; set; }
    public string? ClientId { get; set; }
    public string? BrandCode { get; set; }
    public int Priority { get; set; }
    public bool Enabled { get; set; }
}
