namespace STOTOP.Module.Insurance.Dtos;

/// <summary>
/// 共保基金缴费详情 DTO
/// </summary>
public class InsFundContributionDto
{
    public long Id { get; set; }
    public string Uid { get; set; } = string.Empty;
    public long FundId { get; set; }
    public string? FundName { get; set; }
    public long? PolicyId { get; set; }
    public int BusinessType { get; set; }
    public long RelatedObjectId { get; set; }
    public string? RelatedObjectName { get; set; }
    public string ContributionNumber { get; set; } = string.Empty;
    public decimal ContributionAmount { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public DateOnly? PaymentDate { get; set; }
    public int PaymentStatus { get; set; }
    public string? Remark { get; set; }
    public long? CreatorId { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
}

/// <summary>
/// 共保基金缴费列表项 DTO
/// </summary>
public class InsFundContributionListItemDto
{
    public long Id { get; set; }
    public string ContributionNumber { get; set; } = string.Empty;
    public string? FundName { get; set; }
    public string? RelatedObjectName { get; set; }
    public decimal ContributionAmount { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public DateOnly? PaymentDate { get; set; }
    public int PaymentStatus { get; set; }
}

/// <summary>
/// 创建缴费记录请求
/// </summary>
public class CreateInsFundContributionRequest
{
    public long FundId { get; set; }
    public long? PolicyId { get; set; }
    public int BusinessType { get; set; }
    public long RelatedObjectId { get; set; }
    public string? RelatedObjectName { get; set; }
    public decimal ContributionAmount { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public DateOnly? PaymentDate { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 更新缴费记录请求
/// </summary>
public class UpdateInsFundContributionRequest
{
    public decimal ContributionAmount { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public DateOnly? PaymentDate { get; set; }
    public int PaymentStatus { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 缴费记录查询请求
/// </summary>
public class InsFundContributionQueryRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public long? FundId { get; set; }
    public int? BusinessType { get; set; }
    public long? RelatedObjectId { get; set; }
    public int? PaymentStatus { get; set; }
}
