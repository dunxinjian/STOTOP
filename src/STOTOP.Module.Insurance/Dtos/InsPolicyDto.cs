namespace STOTOP.Module.Insurance.Dtos;

/// <summary>
/// 保单详情 DTO
/// </summary>
public class InsPolicyDto
{
    public long Id { get; set; }
    public string Uid { get; set; } = string.Empty;
    public int BusinessType { get; set; }
    public long RelatedObjectId { get; set; }
    public string? RelatedObjectName { get; set; }
    public int InsuranceCategory { get; set; }
    public string? InsuranceType { get; set; }
    public long? InsuranceCompanyId { get; set; }
    public string? InsuranceCompanyName { get; set; }
    public string? PolicyNumber { get; set; }
    public decimal? Premium { get; set; }
    public decimal? InsuredAmount { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public long? CoInsuranceFundId { get; set; }
    public string? CoInsuranceFundName { get; set; }
    public string? ParticipationNumber { get; set; }
    public int? PaymentCycle { get; set; }
    public decimal? PerPeriodAmount { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public DateOnly ExpiryDate { get; set; }
    public int InsuranceStatus { get; set; }
    public string? Remark { get; set; }
    public long? CreatorId { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
}

/// <summary>
/// 保单列表项 DTO
/// </summary>
public class InsPolicyListItemDto
{
    public long Id { get; set; }
    public int BusinessType { get; set; }
    public string? RelatedObjectName { get; set; }
    public int InsuranceCategory { get; set; }
    public string? InsuranceType { get; set; }
    public string? InsuranceCompanyName { get; set; }
    public string? PolicyNumber { get; set; }
    public decimal? Premium { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public DateOnly ExpiryDate { get; set; }
    public int InsuranceStatus { get; set; }
}

/// <summary>
/// 创建保单请求
/// </summary>
public class CreateInsPolicyRequest
{
    public int BusinessType { get; set; }
    public long RelatedObjectId { get; set; }
    public string? RelatedObjectName { get; set; }
    public int InsuranceCategory { get; set; }
    public string? InsuranceType { get; set; }
    public long? InsuranceCompanyId { get; set; }
    public string? PolicyNumber { get; set; }
    public decimal? Premium { get; set; }
    public decimal? InsuredAmount { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public long? CoInsuranceFundId { get; set; }
    public string? ParticipationNumber { get; set; }
    public int? PaymentCycle { get; set; }
    public decimal? PerPeriodAmount { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public DateOnly ExpiryDate { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 更新保单请求
/// </summary>
public class UpdateInsPolicyRequest
{
    public string? InsuranceType { get; set; }
    public long? InsuranceCompanyId { get; set; }
    public string? PolicyNumber { get; set; }
    public decimal? Premium { get; set; }
    public decimal? InsuredAmount { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public long? CoInsuranceFundId { get; set; }
    public string? ParticipationNumber { get; set; }
    public int? PaymentCycle { get; set; }
    public decimal? PerPeriodAmount { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public DateOnly ExpiryDate { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 保单查询请求
/// </summary>
public class InsPolicyQueryRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int? BusinessType { get; set; }
    public long? RelatedObjectId { get; set; }
    public int? InsuranceCategory { get; set; }
    public string? InsuranceType { get; set; }
    public int? InsuranceStatus { get; set; }
    public long? InsuranceCompanyId { get; set; }
}
