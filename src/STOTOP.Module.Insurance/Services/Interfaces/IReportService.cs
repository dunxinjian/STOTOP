namespace STOTOP.Module.Insurance.Services.Interfaces;

public interface IReportService
{
    Task<List<PremiumSummaryItem>> GetPremiumSummaryAsync(long orgId, int? businessType, int? insuranceCategory, DateOnly? startDate, DateOnly? endDate);
    Task<List<ClaimAnalysisItem>> GetClaimAnalysisAsync(long orgId, int? accidentType, int? businessType, DateOnly? startDate, DateOnly? endDate);
    Task<List<SettlementAnalysisItem>> GetSettlementAnalysisAsync(long orgId, int? settlementType, int? settlementStatus, DateOnly? startDate, DateOnly? endDate);
    Task<List<FundBalanceItem>> GetFundBalanceAsync(long orgId, long? fundId, DateOnly? startDate, DateOnly? endDate);
    Task<InsuranceOverviewDto> GetOverviewAsync(long orgId);
}

#region 报表 DTO

/// <summary>
/// 保费汇总项
/// </summary>
public class PremiumSummaryItem
{
    public int BusinessType { get; set; }
    public int InsuranceCategory { get; set; }
    public string Period { get; set; } = string.Empty;
    public int PolicyCount { get; set; }
    public decimal TotalPremium { get; set; }
    public decimal TotalInsuredAmount { get; set; }
}

/// <summary>
/// 出险分析项
/// </summary>
public class ClaimAnalysisItem
{
    public int AccidentType { get; set; }
    public int BusinessType { get; set; }
    public string Period { get; set; } = string.Empty;
    public int ClaimCount { get; set; }
    public decimal TotalEstimatedLoss { get; set; }
    public decimal TotalActualLoss { get; set; }
}

/// <summary>
/// 赔付分析项
/// </summary>
public class SettlementAnalysisItem
{
    public int SettlementType { get; set; }
    public int SettlementStatus { get; set; }
    public string Period { get; set; } = string.Empty;
    public int SettlementCount { get; set; }
    public decimal TotalAssessedAmount { get; set; }
    public decimal TotalSettlementAmount { get; set; }
}

/// <summary>
/// 基金收支项
/// </summary>
public class FundBalanceItem
{
    public long FundId { get; set; }
    public string FundName { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public decimal Contributions { get; set; }
    public decimal Payouts { get; set; }
    public decimal Balance { get; set; }
}

/// <summary>
/// 综合看板
/// </summary>
public class InsuranceOverviewDto
{
    public int ActivePolicyCount { get; set; }
    public int ExpiringPolicyCount { get; set; }
    public int OngoingClaimCount { get; set; }
    public int PendingSettlementCount { get; set; }
    public decimal TotalFundBalance { get; set; }
    public decimal TotalPremium { get; set; }
    public decimal TotalPayout { get; set; }
}

#endregion
