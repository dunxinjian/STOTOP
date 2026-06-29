namespace STOTOP.Module.Insurance.Dtos;

/// <summary>
/// 出险记录详情 DTO
/// </summary>
public class InsClaimDto
{
    public long Id { get; set; }
    public string Uid { get; set; } = string.Empty;
    public long? PolicyId { get; set; }
    public int BusinessType { get; set; }
    public long RelatedObjectId { get; set; }
    public string? RelatedObjectName { get; set; }
    public string ClaimNumber { get; set; } = string.Empty;
    public DateOnly ClaimDate { get; set; }
    public string? ClaimLocation { get; set; }
    public int AccidentType { get; set; }
    public string? AccidentDescription { get; set; }
    public string? CounterpartyInfo { get; set; }
    public decimal? EstimatedLoss { get; set; }
    public decimal? ActualLoss { get; set; }
    public int? LiabilityDivision { get; set; }
    public long? PartyId { get; set; }
    public string? PartyName { get; set; }
    public string? CaseNumber { get; set; }
    public string? ClaimImages { get; set; }
    public int ClaimStatus { get; set; }
    public DateTime? ClosedDate { get; set; }
    public string? ClosedRemark { get; set; }
    public string? Remark { get; set; }
    public long? CreatorId { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
}

/// <summary>
/// 出险记录列表项 DTO
/// </summary>
public class InsClaimListItemDto
{
    public long Id { get; set; }
    public string ClaimNumber { get; set; } = string.Empty;
    public int BusinessType { get; set; }
    public string? RelatedObjectName { get; set; }
    public DateOnly ClaimDate { get; set; }
    public int AccidentType { get; set; }
    public decimal? EstimatedLoss { get; set; }
    public decimal? ActualLoss { get; set; }
    public int ClaimStatus { get; set; }
}

/// <summary>
/// 创建出险记录请求
/// </summary>
public class CreateInsClaimRequest
{
    public long? PolicyId { get; set; }
    public int BusinessType { get; set; }
    public long RelatedObjectId { get; set; }
    public string? RelatedObjectName { get; set; }
    public DateOnly ClaimDate { get; set; }
    public string? ClaimLocation { get; set; }
    public int AccidentType { get; set; }
    public string? AccidentDescription { get; set; }
    public string? CounterpartyInfo { get; set; }
    public decimal? EstimatedLoss { get; set; }
    public int? LiabilityDivision { get; set; }
    public long? PartyId { get; set; }
    public string? PartyName { get; set; }
    public string? CaseNumber { get; set; }
    public string? ClaimImages { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 更新出险记录请求
/// </summary>
public class UpdateInsClaimRequest
{
    public string? ClaimLocation { get; set; }
    public int AccidentType { get; set; }
    public string? AccidentDescription { get; set; }
    public string? CounterpartyInfo { get; set; }
    public decimal? EstimatedLoss { get; set; }
    public decimal? ActualLoss { get; set; }
    public int? LiabilityDivision { get; set; }
    public long? PartyId { get; set; }
    public string? PartyName { get; set; }
    public string? CaseNumber { get; set; }
    public string? ClaimImages { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 出险记录查询请求
/// </summary>
public class InsClaimQueryRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int? BusinessType { get; set; }
    public long? RelatedObjectId { get; set; }
    public int? AccidentType { get; set; }
    public int? ClaimStatus { get; set; }
    public long? PolicyId { get; set; }
}
