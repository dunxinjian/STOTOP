using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Dtos;

// === 方案 DTO ===
public class CostPlanListDto
{
    public long Id { get; set; }
    public string BrandCode { get; set; } = string.Empty;
    public string? BrandName { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public int Status { get; set; }
    public int ItemCount { get; set; }
    public DateTime CreatedTime { get; set; }
}

public class CostPlanDetailDto
{
    public long Id { get; set; }
    public string BrandCode { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public int Status { get; set; }
    public long OrgId { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
    public List<CostPlanItemDto> Items { get; set; } = new();
    public List<CostPlanExclusionDto> Exclusions { get; set; } = new();
}

public class CostPlanItemDto
{
    public long Id { get; set; }
    public long PlanId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int ItemType { get; set; }
    public int? SettlementWeightStage { get; set; }
    public int SortOrder { get; set; }
    public List<long> OutletIds { get; set; } = new();
    public List<string> ShopNames { get; set; } = new();
    public int PeriodCount { get; set; }
}

public class CostPlanItemPeriodDto
{
    public long Id { get; set; }
    public long ItemId { get; set; }
    public DateTime EffectiveDate { get; set; }
    public string? MatrixJson { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
}

public class CostPlanExclusionDto
{
    public long Id { get; set; }
    public long PlanId { get; set; }
    public DateTime EffectiveDate { get; set; }
    public string? ExclusionRuleJson { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
}

// === 请求 DTO ===
public class CostPlanQueryRequest : PagedRequest
{
    public string? BrandCode { get; set; }
    public int? Status { get; set; }
}

public class CreatePlanRequest
{
    public string BrandCode { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
}

public class UpdatePlanRequest
{
    public string BrandCode { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
}

public class CreateItemRequest
{
    public string ItemName { get; set; } = string.Empty;
    public int ItemType { get; set; } = 1;
    public int? SettlementWeightStage { get; set; }
    public int SortOrder { get; set; } = 0;
}

public class UpdateItemRequest
{
    public string ItemName { get; set; } = string.Empty;
    public int ItemType { get; set; }
    public int? SettlementWeightStage { get; set; }
    public int SortOrder { get; set; } = 0;
}

public class CreatePeriodRequest
{
    public DateTime EffectiveDate { get; set; }
    public string? MatrixJson { get; set; }
}

public class UpdatePeriodRequest
{
    public string? MatrixJson { get; set; }
}

public class CreateExclusionRequest
{
    public DateTime EffectiveDate { get; set; }
    public string? ExclusionRuleJson { get; set; }
}

public class UpdateExclusionRequest
{
    public string? ExclusionRuleJson { get; set; }
}

// === 运单成本计算结果 ===
public class EffectiveCostResult
{
    public string Mode { get; set; } = string.Empty; // "fixed_price" | "standard"
    public decimal TotalCost { get; set; }
    public List<CostBreakdownItem> Breakdowns { get; set; } = new();
}

public class CostBreakdownItem
{
    public long ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int ItemType { get; set; }
    public decimal Amount { get; set; }
}

// === 城市 DTO ===
public class CityDto
{
    public int Id { get; set; }
    public string CityName { get; set; } = string.Empty;
    public int ProvinceId { get; set; }
    public string ProvinceName { get; set; } = string.Empty;
}

// === 矩阵保存/读取 DTO ===

/// <summary>
/// 保存成本项矩阵请求（对应前端 CostItemInput）
/// </summary>
public class SaveItemMatrixRequest
{
    /// <summary>成本项ID</summary>
    public int CostItemId { get; set; }
    /// <summary>定价范围：national / province / city</summary>
    public string PricingScope { get; set; } = "province";
    /// <summary>生效日期（用于定位时间段）</summary>
    public string? EffectiveDate { get; set; }
    /// <summary>重量段列表</summary>
    public List<CostSegmentRequest> Segments { get; set; } = new();
}

/// <summary>
/// 重量段请求（对应前端 CostSegmentInput）
/// </summary>
public class CostSegmentRequest
{
    public int SegmentIndex { get; set; }
    public decimal? WeightFrom { get; set; }
    public decimal? WeightTo { get; set; }
    public int CalcMethod { get; set; }
    public int RoundingMethod { get; set; } = 1;
    public decimal? TruncParam { get; set; }
    public decimal? CeilParam { get; set; }
    public List<CostCellRequest> Cells { get; set; } = new();
}

/// <summary>
/// 单元格请求（对应前端 CostCellInput）
/// </summary>
public class CostCellRequest
{
    public int? ProvinceId { get; set; }
    public int? CityId { get; set; }
    public string? CityName { get; set; }
    public decimal BasePrice { get; set; }
    public decimal ContinuePrice { get; set; }
    public decimal FirstWeight { get; set; }
    public decimal ContinueStep { get; set; } = 1;
    public int? RoundingMethodOverride { get; set; }
    public decimal? TruncParamOverride { get; set; }
    public decimal? CeilParamOverride { get; set; }
}

/// <summary>
/// 成本项矩阵读取响应（对应前端 CostItemEntryDto）
/// </summary>
public class CostItemMatrixDto
{
    public int CostItemId { get; set; }
    public string? CostItemName { get; set; }
    /// <summary>定价范围：national / province / city</summary>
    public string PricingScope { get; set; } = "province";
    public List<CostSegmentDto> Segments { get; set; } = new();
}

/// <summary>
/// 重量段 DTO（对应前端 CostSegmentDto）
/// </summary>
public class CostSegmentDto
{
    public int SegmentIndex { get; set; }
    public decimal? WeightFrom { get; set; }
    public decimal? WeightTo { get; set; }
    public int CalcMethod { get; set; }
    public int RoundingMethod { get; set; } = 1;
    public decimal? TruncParam { get; set; }
    public decimal? CeilParam { get; set; }
    public List<CostCellDto> Cells { get; set; } = new();
}

/// <summary>
/// 单元格 DTO（对应前端 CostCellDto）
/// </summary>
public class CostCellDto
{
    public int? ProvinceId { get; set; }
    public int? CityId { get; set; }
    public string? CityName { get; set; }
    public decimal BasePrice { get; set; }
    public decimal ContinuePrice { get; set; }
    public decimal FirstWeight { get; set; }
    public decimal ContinueStep { get; set; } = 1;
    public int? RoundingMethodOverride { get; set; }
    public decimal? TruncParamOverride { get; set; }
    public decimal? CeilParamOverride { get; set; }
}
