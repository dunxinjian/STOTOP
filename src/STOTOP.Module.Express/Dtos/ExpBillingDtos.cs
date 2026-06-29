using STOTOP.Core.Models;

namespace STOTOP.Module.Express.Dtos;

/// <summary>
/// 计费结果详情
/// </summary>
public class BillingResultDto
{
    public long Id { get; set; }
    public long WaybillId { get; set; }
    public string? WaybillNo { get; set; }
    public DateTime? WaybillDate { get; set; }
    public string PartyClientId { get; set; } = string.Empty;
    public string? PartyClientName { get; set; }
    public int? PartyRole { get; set; }
    public int? ChainLevel { get; set; }
    public string? BrandCode { get; set; }
    public DateTime? BillingDate { get; set; }
    public decimal? BillableWeight { get; set; }
    public decimal? FreightCharge { get; set; }
    public decimal? InsuranceFee { get; set; }
    public decimal? SurchargeAmount { get; set; }
    public decimal? WaiverAmount { get; set; }
    public decimal? CommissionAmount { get; set; }
    public decimal? ChargeAmount { get; set; }
    public long? QuotationId { get; set; }
    public string? ClientType { get; set; }
    public string? QuotationCode { get; set; }
    public long? CommissionRuleId { get; set; }
    public int CalcStatus { get; set; }
    public string? ErrorMessage { get; set; }
    public long? InvoiceId { get; set; }
    public List<BillingCostBreakdownDto> CostBreakdowns { get; set; } = new();
}

/// <summary>
/// 计费成本明细
/// </summary>
public class BillingCostBreakdownDto
{
    public long Id { get; set; }
    public int CostItemId { get; set; }
    public string? CostItemName { get; set; }
    public decimal Amount { get; set; }
}

/// <summary>
/// 计费结果列表项
/// </summary>
public class BillingResultListItemDto
{
    public long Id { get; set; }
    public long WaybillId { get; set; }
    public string? WaybillNo { get; set; }
    public DateTime? WaybillDate { get; set; }
    public string? PartyClientName { get; set; }
    public int? PartyRole { get; set; }
    public decimal? ChargeAmount { get; set; }
    public int CalcStatus { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 计费结果分页查询
/// </summary>
public class BillingResultQueryRequest : PagedRequest
{
    public DateTime? WaybillDate { get; set; }
    public string? PartyClientId { get; set; }
    public string? BrandCode { get; set; }
    public int? CalcStatus { get; set; }
    public int? PartyRole { get; set; }
}

/// <summary>
/// 计费引擎标准化运单输入（从 STG 暂存表映射）
/// </summary>
public class BillingWaybillData
{
    /// <summary>STG表的FID</summary>
    public long RowId { get; set; }
    /// <summary>运单编号</summary>
    public string WaybillNo { get; set; } = string.Empty;
    /// <summary>品牌编码（从规则配置注入）</summary>
    public string BrandCode { get; set; } = string.Empty;
    /// <summary>店铺名称</summary>
    public string ShopName { get; set; } = string.Empty;
    /// <summary>业务日期</summary>
    public DateTime WaybillDate { get; set; }
    /// <summary>共享别名</summary>
    public string? ClientAlias { get; set; }
    /// <summary>目的省份（文本）</summary>
    public string? DestinationProvince { get; set; }
    /// <summary>省份ID（查表后填充）</summary>
    public int? DestinationProvinceId { get; set; }
    /// <summary>目的城市（文本）</summary>
    public string? DestinationCityName { get; set; }
    // 重量字段（全部可选，取有值的最大者或结算重量）
    /// <summary>揽收重量</summary>
    public decimal? PickupWeight { get; set; }
    /// <summary>中转重量</summary>
    public decimal? TransitWeight { get; set; }
    /// <summary>到件重量</summary>
    public decimal? DeliveryWeight { get; set; }
    /// <summary>集包重量</summary>
    public decimal? BundleWeight { get; set; }
    /// <summary>计泡重量</summary>
    public decimal? VolumeWeight { get; set; }
    /// <summary>总部重量</summary>
    public decimal? HqWeight { get; set; }
    /// <summary>结算重量</summary>
    public decimal? SettlementWeight { get; set; }
    /// <summary>声明价值（保价金额）</summary>
    public decimal? DeclarationValue { get; set; }
    /// <summary>计费状态</summary>
    public int BillingStatus { get; set; }
    /// <summary>所属网点名称（从暂存表 F所属网点 读取）</summary>
    public string? NetworkPointName { get; set; }
    /// <summary>网点编号（匹配后赋值）</summary>
    public string? NetworkPointCode { get; set; }
    /// <summary>组织ID</summary>
    public long OrgId { get; set; }
}

/// <summary>
/// 计费执行请求（手动触发）
/// </summary>
public class BillingExecutionRequest
{
    /// <summary>标准化运单数据列表</summary>
    public List<BillingWaybillData> Waybills { get; set; } = new();
    /// <summary>来源暂存表名</summary>
    public string SourceTable { get; set; } = string.Empty;
    /// <summary>批次ID</summary>
    public long BatchId { get; set; }
    /// <summary>结果存储表名</summary>
    public string ResultTable { get; set; } = string.Empty;
}

/// <summary>
/// 计费执行结果
/// </summary>
public class BillingExecutionResult
{
    public int TotalWaybills { get; set; }
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
    public int BillingResultsCreated { get; set; }
    public TimeSpan Duration { get; set; }
    public List<BillingError> Errors { get; set; } = new();

    /// <summary>部分失败记录是否写入异常</summary>
    public bool FailureRecordSaveError { get; set; }

    /// <summary>未保存的失败记录数</summary>
    public int FailureRecordUnsavedCount { get; set; }

    /// <summary>运单网点与报价网点不一致的记录</summary>
    public List<NetworkPointMismatchInfo> NetworkPointMismatches { get; set; } = new();
}

/// <summary>
/// 运单网点与报价网点不一致信息
/// </summary>
public class NetworkPointMismatchInfo
{
    public string WaybillNo { get; set; } = string.Empty;
    public long RowId { get; set; }
    public string WaybillNpCode { get; set; } = string.Empty;
    public string QuotationNpCode { get; set; } = string.Empty;
}

/// <summary>
/// 计费错误
/// </summary>
public class BillingError
{
    public long WaybillId { get; set; }
    public string WaybillNo { get; set; } = string.Empty;
    public string ShopName { get; set; } = string.Empty;
    public string ErrorCode { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}

// ===== 异常运单查询相关 =====

/// <summary>
/// 异常运单统计响应
/// </summary>
public class BillingErrorStatsDto
{
    public List<BillingErrorGroupDto> Groups { get; set; } = new();
    public int TotalErrorWaybills { get; set; }
}

/// <summary>
/// 异常运单分组
/// </summary>
public class BillingErrorGroupDto
{
    public string ErrorCode { get; set; } = string.Empty;
    public string ErrorName { get; set; } = string.Empty;
    public int WaybillCount { get; set; }
    public List<string> ShopNames { get; set; } = new();
    public BillingErrorDateRange DateRange { get; set; } = new();
}

public class BillingErrorDateRange
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}

/// <summary>
/// 异常运单明细查询请求
/// </summary>
public class BillingErrorDetailRequest : PagedRequest
{
    /// <summary>异常编码（必填）</summary>
    public string ErrorCode { get; set; } = string.Empty;
}

/// <summary>
/// 异常运单明细项
/// </summary>
public class BillingErrorDetailItemDto
{
    public long WaybillId { get; set; }
    public string WaybillNo { get; set; } = string.Empty;
    public string? ShopName { get; set; }
    public string? BrandName { get; set; }
    public DateTime WaybillDate { get; set; }
    public string ErrorCode { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 重算请求
/// </summary>
public class BillingRetryRequest
{
    /// <summary>按异常类型重算</summary>
    public string? ErrorCode { get; set; }
    /// <summary>按店铺名重算</summary>
    public List<string>? ShopNames { get; set; }
    /// <summary>按运单ID重算</summary>
    public List<long>? WaybillIds { get; set; }
}

/// <summary>
/// 重算结果
/// </summary>
public class BillingRetryResultDto
{
    public int ResetCount { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public string Message { get; set; } = string.Empty;
}
