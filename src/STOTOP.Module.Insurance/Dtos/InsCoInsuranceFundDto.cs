namespace STOTOP.Module.Insurance.Dtos;

/// <summary>
/// 共保基金详情 DTO
/// </summary>
public class InsCoInsuranceFundDto
{
    public long Id { get; set; }
    public string Uid { get; set; } = string.Empty;
    public string FundName { get; set; } = string.Empty;
    public string FundCode { get; set; } = string.Empty;
    public int BusinessType { get; set; }
    public string? FundDescription { get; set; }
    public decimal TotalContributions { get; set; }
    public decimal TotalPayouts { get; set; }
    public decimal FundBalance { get; set; }
    public decimal? ContributionStandard { get; set; }
    public int? PaymentCycle { get; set; }
    public decimal Deductible { get; set; }
    public decimal? SinglePayoutLimit { get; set; }
    public decimal? AnnualPayoutLimit { get; set; }
    public int FundStatus { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public string? Remark { get; set; }
    public long? CreatorId { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
}

/// <summary>
/// 共保基金列表项 DTO
/// </summary>
public class InsCoInsuranceFundListItemDto
{
    public long Id { get; set; }
    public string FundName { get; set; } = string.Empty;
    public string FundCode { get; set; } = string.Empty;
    public int BusinessType { get; set; }
    public decimal FundBalance { get; set; }
    public decimal TotalContributions { get; set; }
    public decimal TotalPayouts { get; set; }
    public int FundStatus { get; set; }
    public DateOnly EffectiveDate { get; set; }
}

/// <summary>
/// 创建共保基金请求
/// </summary>
public class CreateInsCoInsuranceFundRequest
{
    public string FundName { get; set; } = string.Empty;
    public string FundCode { get; set; } = string.Empty;
    public int BusinessType { get; set; } = 1;
    public string? FundDescription { get; set; }
    public decimal? ContributionStandard { get; set; }
    public int? PaymentCycle { get; set; }
    public decimal Deductible { get; set; }
    public decimal? SinglePayoutLimit { get; set; }
    public decimal? AnnualPayoutLimit { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 更新共保基金请求
/// </summary>
public class UpdateInsCoInsuranceFundRequest
{
    public string FundName { get; set; } = string.Empty;
    public string? FundDescription { get; set; }
    public decimal? ContributionStandard { get; set; }
    public int? PaymentCycle { get; set; }
    public decimal Deductible { get; set; }
    public decimal? SinglePayoutLimit { get; set; }
    public decimal? AnnualPayoutLimit { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 共保基金查询请求
/// </summary>
public class InsCoInsuranceFundQueryRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Keyword { get; set; }
    public int? BusinessType { get; set; }
    public int? FundStatus { get; set; }
}
