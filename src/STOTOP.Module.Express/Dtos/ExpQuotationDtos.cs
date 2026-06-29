using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Dtos;

/// <summary>
/// 报价方案详情（含重量段和矩阵明细）
/// </summary>
public class QuotationDto
{
    public long Id { get; set; }
    public string BrandCode { get; set; } = string.Empty;
    public string? BrandName { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public int SettlementWeightStage { get; set; }
    public int Status { get; set; }
    public int Version { get; set; }
    public long? OaProcessId { get; set; }
    public long? PreviousPlanId { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
    // 新增字段
    public string? PlanCode { get; set; }        // F方案编号
    public string? NetworkPointCode { get; set; } // F网点编号
    public string? ClientType { get; set; }       // F业务对象类型
    public string? ClientId { get; set; }         // F业务对象ID
    public DateOnly? EffectiveDate { get; set; }  // F生效日期
    public bool SharedShopEnabled { get; set; }   // F共享店铺开关
    public int? WeightRoundingMethod { get; set; } // F重量进位方式
    // 商务条款
    public int PaymentMode { get; set; } = 2;
    public decimal? PrepayRatio { get; set; }
    public int BillingCycle { get; set; } = 2;
    public int? BillingDay { get; set; }
    public int? PaymentDueDay { get; set; }
    public int ThrowRatio { get; set; } = 8000;
    public decimal? InsuranceRate { get; set; }
    public string? Remark { get; set; }
    // 关联店铺
    public List<QuotationShopDto> Shops { get; set; } = new();
    public List<WeightSegmentDto> Segments { get; set; } = new();
    public List<PriceCellDto> Cells { get; set; } = new();
}

/// <summary>
/// 报价方案列表项
/// </summary>
public class QuotationListItemDto
{
    public long Id { get; set; }
    public string BrandCode { get; set; } = string.Empty;
    public string? BrandName { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string? PlanCode { get; set; }
    public string? NetworkPointCode { get; set; }
    public string? ClientType { get; set; }
    public string? ClientId { get; set; }
    public DateOnly? EffectiveDate { get; set; }
    public bool SharedShopEnabled { get; set; }
    public int SettlementWeightStage { get; set; }
    public int Status { get; set; }
    public int Version { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime UpdatedTime { get; set; }
    /// <summary>关联店铺数量</summary>
    public int ShopCount { get; set; }
}

/// <summary>
/// 报价方案关联店铺
/// </summary>
public class QuotationShopDto
{
    public long Id { get; set; }
    public long QuotationId { get; set; }
    public string? ShopName { get; set; }
    public string? Remark { get; set; }
    public DateTime? CreatedTime { get; set; }
}

/// <summary>
/// 添加关联店铺请求
/// </summary>
public class AddQuotationShopsRequest
{
    public List<string> ShopNames { get; set; } = new();
    public string? Remark { get; set; }
}

/// <summary>
/// 重量段DTO
/// </summary>
public class WeightSegmentDto
{
    public long Id { get; set; }
    public int SegmentIndex { get; set; }
    public decimal? WeightFrom { get; set; }
    public decimal? WeightTo { get; set; }
    public int PricingMethod { get; set; }
    public decimal? FirstWeight { get; set; }
    public decimal? ContinueWeight { get; set; }
    public int RoundingMethod { get; set; } = 1;
    public decimal? TruncParam { get; set; }
    public decimal? CeilParam { get; set; }
    public decimal? RoundingParam { get; set; }
}

/// <summary>
/// 快递报价矩阵明细DTO
/// </summary>
public class PriceCellDto
{
    public long Id { get; set; }
    public long SegmentId { get; set; }
    public int ProvinceId { get; set; }
    /// <summary>基础价格（必填）。ContinuePrice IS NULL → 固定单价；非空 → 首续重。</summary>
    public decimal BasePrice { get; set; }
    /// <summary>续重价格。NULL=固定单价；非空=首续重。</summary>
    public decimal? ContinuePrice { get; set; }
    public decimal? FirstWeight { get; set; }
    public decimal? ContinueStep { get; set; }
    public int? RoundingMethodOverride { get; set; }
    public decimal? TruncParamOverride { get; set; }
    public decimal? CeilParamOverride { get; set; }
    public decimal? FirstWeightOverride { get; set; }
    public decimal? ContinueStepOverride { get; set; }
    public decimal? RoundingParamOverride { get; set; }
}

/// <summary>
/// 创建报价方案请求
/// </summary>
public class CreateQuotationRequest
{
    public string BrandCode { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public int SettlementWeightStage { get; set; } = 1;
    public DateOnly? EffectiveDate { get; set; }
    public bool AllowIncomplete { get; set; } = false;
    // 业务对象绑定
    public string? PlanCode { get; set; }
    public string? ClientType { get; set; }
    public string? ClientId { get; set; }
    public string? NetworkPointCode { get; set; }
    public bool SharedShopEnabled { get; set; }
    public int? WeightRoundingMethod { get; set; }
    // 商务条款
    public int PaymentMode { get; set; } = 2;
    public decimal? PrepayRatio { get; set; }
    public int BillingCycle { get; set; } = 2;
    public int? BillingDay { get; set; }
    public int? PaymentDueDay { get; set; }
    public int ThrowRatio { get; set; } = 8000;
    public decimal? InsuranceRate { get; set; }
    public string? Remark { get; set; }
    public List<WeightSegmentInput> Segments { get; set; } = new();
}

/// <summary>
/// 更新报价方案请求
/// </summary>
public class UpdateQuotationRequest
{
    public string PlanName { get; set; } = string.Empty;
    public int SettlementWeightStage { get; set; } = 1;
    public DateOnly? EffectiveDate { get; set; }
    public bool AllowIncomplete { get; set; } = false;
    // 业务对象绑定
    public string? PlanCode { get; set; }
    public string? ClientType { get; set; }
    public string? ClientId { get; set; }
    public string? NetworkPointCode { get; set; }
    public bool SharedShopEnabled { get; set; }
    public int? WeightRoundingMethod { get; set; }
    // 商务条款
    public int PaymentMode { get; set; } = 2;
    public decimal? PrepayRatio { get; set; }
    public int BillingCycle { get; set; } = 2;
    public int? BillingDay { get; set; }
    public int? PaymentDueDay { get; set; }
    public int ThrowRatio { get; set; } = 8000;
    public decimal? InsuranceRate { get; set; }
    public string? Remark { get; set; }
    public List<WeightSegmentInput> Segments { get; set; } = new();
}

/// <summary>
/// 重量段输入
/// </summary>
public class WeightSegmentInput
{
    public int SegmentIndex { get; set; }
    public decimal? WeightFrom { get; set; }
    public decimal? WeightTo { get; set; }
    public int PricingMethod { get; set; }
    public decimal? FirstWeight { get; set; }
    public decimal? ContinueWeight { get; set; }
    public int RoundingMethod { get; set; } = 1;
    public decimal? TruncParam { get; set; }
    public decimal? CeilParam { get; set; }
    public decimal? RoundingParam { get; set; }
    public List<PriceCellInput> Cells { get; set; } = new();
}

/// <summary>
/// 快递报价矩阵明细输入
/// </summary>
public class PriceCellInput
{
    public int ProvinceId { get; set; }
    /// <summary>基础价格（必填）。ContinuePrice IS NULL → 固定单价；非空 → 首续重。</summary>
    public decimal BasePrice { get; set; }
    /// <summary>续重价格。NULL=固定单价；非空=首续重。</summary>
    public decimal? ContinuePrice { get; set; }
    public decimal? FirstWeight { get; set; }
    public decimal? ContinueStep { get; set; }
    public int? RoundingMethodOverride { get; set; }
    public decimal? TruncParamOverride { get; set; }
    public decimal? CeilParamOverride { get; set; }
    public decimal? FirstWeightOverride { get; set; }
    public decimal? ContinueStepOverride { get; set; }
    public decimal? RoundingParamOverride { get; set; }
}

/// <summary>
/// 报价方案查询请求
/// </summary>
public class QuotationQueryRequest : PagedRequest
{
    public string? BrandCode { get; set; }
    public int? Status { get; set; }
    public string? ClientType { get; set; }
    public string? ClientId { get; set; }
}

// ==================== 佣金配置 ====================

/// <summary>
/// 佣金配置 DTO
/// </summary>
public class QuotationCommissionDto
{
    public long FId { get; set; }
    public long FQuotationId { get; set; }
    public bool FEnabled { get; set; }
    /// <summary>计算方式: percent/fixed/weight</summary>
    public string FCalcMethod { get; set; } = "fixed";
    public decimal? FRate { get; set; }
    public decimal? FFixedAmount { get; set; }
    public decimal? FWeightAmount { get; set; }
    public string FTargetClientType { get; set; } = string.Empty;
    public string? FTargetClientId { get; set; }
    public DateTime FCreatedTime { get; set; }
}

/// <summary>
/// 保存佣金配置请求
/// </summary>
public class SaveQuotationCommissionRequest
{
    public long? FId { get; set; }
    public bool FEnabled { get; set; } = true;
    /// <summary>计算方式: percent/fixed/weight</summary>
    public string FCalcMethod { get; set; } = "fixed";
    public decimal? FRate { get; set; }
    public decimal? FFixedAmount { get; set; }
    public decimal? FWeightAmount { get; set; }
    public string FTargetClientType { get; set; } = string.Empty;
    public string? FTargetClientId { get; set; }
}

// ==================== 别名管理 ====================

/// <summary>
/// 报价别名 DTO
/// </summary>
public class QuotationAliasDto
{
    public long Id { get; set; }
    public long QuotationId { get; set; }
    public string Alias { get; set; } = string.Empty;
    public string? BrandCode { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Remark { get; set; }
    public DateTime CreatedTime { get; set; }
}

/// <summary>
/// 添加报价别名请求
/// </summary>
public class AddQuotationAliasRequest
{
    public string Alias { get; set; } = string.Empty;
}

// ==================== 店铺冲突检查 ====================

/// <summary>
/// 店铺冲突信息
/// </summary>
public class ShopConflictDto
{
    public string ShopName { get; set; } = "";       // 冲突的店铺名称
    public string ClientType { get; set; } = "";     // 业务对象类型代码 KH/DL/WD/YW/CB/YZ
    public string ClientTypeName { get; set; } = ""; // 业务对象类型中文名
    public string ClientId { get; set; } = "";       // 业务对象编号
    public string BrandCode { get; set; } = "";      // 品牌编码
    public string QuotationName { get; set; } = "";  // 报价方案名称
    public DateOnly? EffectiveDate { get; set; }     // 生效日期
}

// ==================== 变更日志 ====================

/// <summary>
/// 报价变更日志 DTO（适配前端字段）
/// </summary>
public class QuotationChangeLogDto
{
    public long FId { get; set; }
    public long FQuotationId { get; set; }
    /// <summary>变更字段描述</summary>
    public string FFieldName { get; set; } = string.Empty;
    /// <summary>变更前值</summary>
    public string? FOldValue { get; set; }
    /// <summary>变更后值</summary>
    public string? FNewValue { get; set; }
    /// <summary>变更人</summary>
    public string? FChangedBy { get; set; }
    /// <summary>变更时间</summary>
    public DateTime FChangedTime { get; set; }
}

// ==================== 按店铺查询报价 ====================

/// <summary>按店铺查询报价 - 分组结果</summary>
public class QuotationByShopGroupDto
{
    /// <summary>业务对象类型编码（KH/DL/WD/CB/YZ/YW，为空表示未分配）</summary>
    public string? ClientType { get; set; }
    /// <summary>业务对象类型名称</summary>
    public string ClientTypeName { get; set; } = string.Empty;
    /// <summary>业务对象编号</summary>
    public string? ClientId { get; set; }
    /// <summary>业务对象名称</summary>
    public string ClientName { get; set; } = string.Empty;
    /// <summary>该对象下的报价列表(按生效日期倒序)</summary>
    public List<QuotationListItemDto> Quotations { get; set; } = new();
}
