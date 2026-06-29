using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Dtos;

/// <summary>
/// 返利方案列表项
/// </summary>
public class PolicyRebateListItemDto
{
    public long Id { get; set; }
    public string BrandCode { get; set; } = string.Empty;
    public string PolicyName { get; set; } = string.Empty;
    public int RebateMode { get; set; }
    public decimal? FlatRebateAmount { get; set; }
    public int SettlementCycle { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public int Status { get; set; }
    public DateTime CreatedTime { get; set; }
}

/// <summary>
/// 返利方案详情（含阶梯+规则+规则明细）
/// </summary>
public class PolicyRebateDetailDto
{
    public long Id { get; set; }
    public string BrandCode { get; set; } = string.Empty;
    public string PolicyName { get; set; } = string.Empty;
    public int RebateMode { get; set; }
    public decimal? FlatRebateAmount { get; set; }
    public int SettlementCycle { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public int Status { get; set; }
    public string? Remark { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
    public List<PolicyRebateTierDto> Tiers { get; set; } = new();
    public List<PolicyRebateRuleDto> Rules { get; set; } = new();
}

/// <summary>
/// 阶梯档位DTO
/// </summary>
public class PolicyRebateTierDto
{
    public long Id { get; set; }
    public int DailyVolumeFrom { get; set; }
    public int? DailyVolumeTo { get; set; }
    public decimal RebatePerTicket { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>
/// 奖罚规则DTO
/// </summary>
public class PolicyRebateRuleDto
{
    public long Id { get; set; }
    public int RuleType { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public int SortOrder { get; set; }
    public List<PolicyRebateRuleItemDto> Items { get; set; } = new();
}

/// <summary>
/// 规则条件明细DTO
/// </summary>
public class PolicyRebateRuleItemDto
{
    public long Id { get; set; }
    public decimal? ThresholdLower { get; set; }
    public decimal? ThresholdUpper { get; set; }
    public decimal? WeightFrom { get; set; }
    public decimal? WeightTo { get; set; }
    public int? ProvinceId { get; set; }
    public int? AdjustType { get; set; }
    public int? AdjustCalcMethod { get; set; }
    public decimal? AdjustAmount { get; set; }
    public decimal? AdjustRate { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>
/// 创建返利方案请求
/// </summary>
public class CreatePolicyRebateRequest
{
    public string BrandCode { get; set; } = string.Empty;
    public string PolicyName { get; set; } = string.Empty;
    public int RebateMode { get; set; }
    public decimal? FlatRebateAmount { get; set; }
    public int SettlementCycle { get; set; } = 3;
    public DateTime EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? Remark { get; set; }
    public List<CreatePolicyRebateTierRequest> Tiers { get; set; } = new();
    public List<CreatePolicyRebateRuleRequest> Rules { get; set; } = new();
}

public class CreatePolicyRebateTierRequest
{
    public int DailyVolumeFrom { get; set; }
    public int? DailyVolumeTo { get; set; }
    public decimal RebatePerTicket { get; set; }
    public int SortOrder { get; set; }
}

public class CreatePolicyRebateRuleRequest
{
    public int RuleType { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public int SortOrder { get; set; }
    public List<CreatePolicyRebateRuleItemRequest> Items { get; set; } = new();
}

public class CreatePolicyRebateRuleItemRequest
{
    public decimal? ThresholdLower { get; set; }
    public decimal? ThresholdUpper { get; set; }
    public decimal? WeightFrom { get; set; }
    public decimal? WeightTo { get; set; }
    public int? ProvinceId { get; set; }
    public int? AdjustType { get; set; }
    public int? AdjustCalcMethod { get; set; }
    public decimal? AdjustAmount { get; set; }
    public decimal? AdjustRate { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>
/// 更新返利方案请求
/// </summary>
public class UpdatePolicyRebateRequest
{
    public string PolicyName { get; set; } = string.Empty;
    public int RebateMode { get; set; }
    public decimal? FlatRebateAmount { get; set; }
    public int SettlementCycle { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? Remark { get; set; }
    public List<CreatePolicyRebateTierRequest> Tiers { get; set; } = new();
    public List<CreatePolicyRebateRuleRequest> Rules { get; set; } = new();
}

/// <summary>
/// 返利方案查询请求
/// </summary>
public class PolicyRebateQueryRequest : PagedRequest
{
    public string? BrandCode { get; set; }
    public int? Status { get; set; }
    public int? RebateMode { get; set; }
}
