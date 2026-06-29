namespace STOTOP.Module.Insurance.Dtos;

/// <summary>
/// 理赔记录详情 DTO
/// </summary>
public class InsSettlementDto
{
    public long Id { get; set; }
    public string Uid { get; set; } = string.Empty;
    public long ClaimId { get; set; }
    public long PolicyId { get; set; }
    public string SettlementNumber { get; set; } = string.Empty;
    public int SettlementType { get; set; }
    public DateOnly ApplyDate { get; set; }
    public long? ApplicantId { get; set; }
    public string? ApplicantName { get; set; }
    public decimal? AssessedAmount { get; set; }
    public decimal? SettlementAmount { get; set; }
    public decimal? SelfPayAmount { get; set; }
    public decimal? Deductible { get; set; }
    public int SettlementStatus { get; set; }
    public long? CurrentStepId { get; set; }
    public string? CurrentStepName { get; set; }
    public DateOnly? PaymentDate { get; set; }
    public string? PaymentVoucher { get; set; }
    public string? Remark { get; set; }
    public long? CreatorId { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
    // 关联信息
    public string? ClaimNumber { get; set; }
    public string? PolicyNumber { get; set; }
}

/// <summary>
/// 理赔记录列表项 DTO
/// </summary>
public class InsSettlementListItemDto
{
    public long Id { get; set; }
    public string SettlementNumber { get; set; } = string.Empty;
    public int SettlementType { get; set; }
    public DateOnly ApplyDate { get; set; }
    public string? ApplicantName { get; set; }
    public decimal? AssessedAmount { get; set; }
    public decimal? SettlementAmount { get; set; }
    public int SettlementStatus { get; set; }
    public string? ClaimNumber { get; set; }
}

/// <summary>
/// 创建理赔记录请求
/// </summary>
public class CreateInsSettlementRequest
{
    public long ClaimId { get; set; }
    public long PolicyId { get; set; }
    public int SettlementType { get; set; }
    public DateOnly ApplyDate { get; set; }
    public long? ApplicantId { get; set; }
    public string? ApplicantName { get; set; }
    public decimal? AssessedAmount { get; set; }
    public decimal? SettlementAmount { get; set; }
    public decimal? SelfPayAmount { get; set; }
    public decimal? Deductible { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 更新理赔记录请求
/// </summary>
public class UpdateInsSettlementRequest
{
    public decimal? AssessedAmount { get; set; }
    public decimal? SettlementAmount { get; set; }
    public decimal? SelfPayAmount { get; set; }
    public decimal? Deductible { get; set; }
    public string? PaymentVoucher { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 理赔记录查询请求
/// </summary>
public class InsSettlementQueryRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public long? ClaimId { get; set; }
    public long? PolicyId { get; set; }
    public int? SettlementType { get; set; }
    public int? SettlementStatus { get; set; }
}
