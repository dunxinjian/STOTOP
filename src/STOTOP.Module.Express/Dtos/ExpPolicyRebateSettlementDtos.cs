using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Dtos;

/// <summary>
/// 结算列表项
/// </summary>
public class PolicyRebateSettlementListItemDto
{
    public long Id { get; set; }
    public long PolicyRebateId { get; set; }
    public string BrandCode { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public int? TotalWaybills { get; set; }
    public decimal? TotalWeight { get; set; }
    public decimal? BaseRebateAmount { get; set; }
    public decimal? FinalRebateAmount { get; set; }
    public int Status { get; set; }
    public DateTime CreatedTime { get; set; }
}

/// <summary>
/// 结算详情
/// </summary>
public class PolicyRebateSettlementDetailDto
{
    public long Id { get; set; }
    public long PolicyRebateId { get; set; }
    public string BrandCode { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public int? TotalWaybills { get; set; }
    public decimal? TotalWeight { get; set; }
    public decimal? AvgWeight { get; set; }
    public decimal? BaseRebateAmount { get; set; }
    public decimal? TotalReward { get; set; }
    public decimal? TotalPenalty { get; set; }
    public decimal? FinalRebateAmount { get; set; }
    public int Status { get; set; }
    public string? ConfirmedBy { get; set; }
    public DateTime? ConfirmedTime { get; set; }
    public string? Remark { get; set; }
    public DateTime CreatedTime { get; set; }
    public List<SettlementAdjustDetailDto> Details { get; set; } = new();
}

/// <summary>
/// 结算奖罚明细DTO
/// </summary>
public class SettlementAdjustDetailDto
{
    public long Id { get; set; }
    public long? RuleId { get; set; }
    public long? RuleItemId { get; set; }
    public decimal? ActualValue { get; set; }
    public decimal? ThresholdValue { get; set; }
    public int? AdjustType { get; set; }
    public decimal? AdjustAmount { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 结算查询请求
/// </summary>
public class SettlementQueryRequest : PagedRequest
{
    public long? PolicyRebateId { get; set; }
    public string? BrandCode { get; set; }
    public int? Status { get; set; }
    public DateTime? PeriodStartFrom { get; set; }
    public DateTime? PeriodStartTo { get; set; }
}

/// <summary>
/// 执行结算请求
/// </summary>
public class ExecuteSettlementRequest
{
    public long PolicyRebateId { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
}

/// <summary>
/// 测算请求
/// </summary>
public class SimulationRequest
{
    public long PolicyRebateId { get; set; }
    /// <summary>true=基于历史数据, false=基于假设数据</summary>
    public bool UseHistory { get; set; } = true;
    public DateTime? PeriodStart { get; set; }
    public DateTime? PeriodEnd { get; set; }
    /// <summary>假设日均单量（UseHistory=false时使用）</summary>
    public int? AssumedDailyVolume { get; set; }
    /// <summary>假设总天数（UseHistory=false时使用）</summary>
    public int? AssumedDays { get; set; }
    /// <summary>假设平均重量（UseHistory=false时使用）</summary>
    public decimal? AssumedAvgWeight { get; set; }
}

/// <summary>
/// 测算结果
/// </summary>
public class SimulationResult
{
    public int TotalWaybills { get; set; }
    public decimal TotalWeight { get; set; }
    public decimal AvgWeight { get; set; }
    public decimal BaseRebateAmount { get; set; }
    public decimal TotalReward { get; set; }
    public decimal TotalPenalty { get; set; }
    public decimal FinalRebateAmount { get; set; }
    public List<SimulationAdjustDetail> Adjustments { get; set; } = new();
}

/// <summary>
/// 测算奖罚明细
/// </summary>
public class SimulationAdjustDetail
{
    public string RuleName { get; set; } = string.Empty;
    public int RuleType { get; set; }
    public decimal ActualValue { get; set; }
    public int AdjustType { get; set; }
    public decimal AdjustAmount { get; set; }
}
