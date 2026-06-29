namespace STOTOP.Module.Finance.Dtos;

// ===== 请求 =====
public class AmoebaReportRequest
{
    public long TemplateId { get; set; }
    public long OrgId { get; set; }
    public long AccountSetId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Granularity { get; set; } = "month";     // "day" | "week" | "month"
    public string ViewMode { get; set; } = "unit";          // "unit" | "site"
    public List<long>? UnitIds { get; set; }
    public List<string>? SiteCodes { get; set; }
    public List<string>? BrandCodes { get; set; }
    public string? Direction { get; set; }
}

// ===== 响应 =====
public class AmoebaReportResponse
{
    public AmoebaOrgSummary OrgSummary { get; set; } = new();
    public List<AmoebaUnitData>? Units { get; set; }
    public List<AmoebaSiteData>? Sites { get; set; }
    public decimal UnclassifiedAmount { get; set; }
    public int UnclassifiedCount { get; set; }
}

public class AmoebaOrgSummary
{
    public string OrgName { get; set; } = "";
    public string PeriodLabel { get; set; } = "";
    public decimal TotalRevenue { get; set; }
    public decimal TotalCost { get; set; }
    public decimal TotalExpense { get; set; }
    public decimal TotalDepreciation { get; set; }
    public decimal TotalProfit { get; set; }
    public decimal ProfitRate { get; set; }
    public int TotalWaybillCount { get; set; }
    public decimal TotalWeight { get; set; }
    public decimal AverageWeight { get; set; }
    public decimal RevenuePerTicket { get; set; }
    public decimal CostPerTicket { get; set; }
    public decimal ProfitPerTicket { get; set; }
    public AmoebaDirectionMetrics InboundTotal { get; set; } = new();
    public AmoebaDirectionMetrics OutboundTotal { get; set; } = new();
    public AmoebaDirectionMetrics GeneralTotal { get; set; } = new();
}

public class AmoebaUnitData
{
    public long UnitId { get; set; }
    public string UnitCode { get; set; } = "";
    public string UnitName { get; set; } = "";
    public long? ParentId { get; set; }
    public AmoebaDirectionMetrics InboundSubtotal { get; set; } = new();
    public AmoebaDirectionMetrics OutboundSubtotal { get; set; } = new();
    public AmoebaDirectionMetrics GeneralSubtotal { get; set; } = new();
    public decimal UnitProfit { get; set; }
    public int UnitWaybillCount { get; set; }
    public decimal UnitTotalWeight { get; set; }
    public decimal UnitAverageWeight { get; set; }
    public decimal UnitRevenuePerTicket { get; set; }
    public decimal UnitCostPerTicket { get; set; }
    public decimal UnitProfitPerTicket { get; set; }
    public List<AmoebaBrandData> Brands { get; set; } = new();
}

public class AmoebaBrandData
{
    public string BrandCode { get; set; } = "";
    public string BrandName { get; set; } = "";
    public AmoebaDirectionMetrics Inbound { get; set; } = new();
    public AmoebaDirectionMetrics Outbound { get; set; } = new();
    public AmoebaDirectionMetrics General { get; set; } = new();
    public decimal BrandRevenue { get; set; }
    public decimal BrandCost { get; set; }
    public decimal BrandProfit { get; set; }
    public int BrandWaybillCount { get; set; }
    public decimal BrandTotalWeight { get; set; }
    public decimal BrandAverageWeight { get; set; }
    public decimal BrandRevenuePerTicket { get; set; }
    public decimal BrandCostPerTicket { get; set; }
    public decimal BrandProfitPerTicket { get; set; }
}

public class AmoebaDirectionMetrics
{
    public string Direction { get; set; } = "";
    public decimal Revenue { get; set; }
    public decimal Cost { get; set; }
    public decimal Expense { get; set; }
    public decimal AllocatedCost { get; set; }
    public decimal Profit { get; set; }
    public decimal ProfitRate { get; set; }
    public int WaybillCount { get; set; }
    public decimal TotalWeight { get; set; }
    public decimal AverageWeight { get; set; }
    public decimal RevenuePerTicket { get; set; }
    public decimal CostPerTicket { get; set; }
    public decimal ProfitPerTicket { get; set; }
    public List<AmoebaPLLineItem>? LineItems { get; set; }
}

public class AmoebaPLLineItem
{
    public long ItemId { get; set; }
    public string ItemName { get; set; } = "";
    public string NodeRole { get; set; } = "";
    public decimal Amount { get; set; }
    public int Depth { get; set; }
}

// 网点视角
public class AmoebaSiteData
{
    public string SiteCode { get; set; } = "";
    public string SiteName { get; set; } = "";
    public decimal SiteRevenue { get; set; }
    public decimal SiteCost { get; set; }
    public decimal SiteProfit { get; set; }
    public int SiteWaybillCount { get; set; }
    public decimal SiteTotalWeight { get; set; }
    public decimal SiteAverageWeight { get; set; }
    public decimal SiteRevenuePerTicket { get; set; }
    public decimal SiteCostPerTicket { get; set; }
    public decimal SiteProfitPerTicket { get; set; }
    public List<AmoebaSiteBrandData> Brands { get; set; } = new();
}

public class AmoebaSiteBrandData
{
    public string BrandCode { get; set; } = "";
    public string BrandName { get; set; } = "";
    public List<AmoebaSiteDirectionData> Directions { get; set; } = new();
    public decimal BrandRevenue { get; set; }
    public decimal BrandCost { get; set; }
    public decimal BrandProfit { get; set; }
    public int BrandWaybillCount { get; set; }
    public decimal BrandAverageWeight { get; set; }
}

public class AmoebaSiteDirectionData
{
    public string Direction { get; set; } = "";
    public long? MappedUnitId { get; set; }
    public string? MappedUnitName { get; set; }
    public AmoebaDirectionMetrics Metrics { get; set; } = new();
}

// 钻取
public class AmoebaDrillDownResponse
{
    public string UnitName { get; set; } = "";
    public string Date { get; set; } = "";
    public string Category { get; set; } = "";
    public List<AmoebaDrillDownItem> Items { get; set; } = new();
}

public class AmoebaDrillDownItem
{
    public string Label { get; set; } = "";
    public string SubLabel { get; set; } = "";
    public decimal Amount { get; set; }
    public string Source { get; set; } = "";
}

// 手工分类
public class AmoebaUnclassifiedDto
{
    public long EntryId { get; set; }
    public DateTime Date { get; set; }
    public string AccountName { get; set; } = "";
    public string Summary { get; set; } = "";
    public decimal Amount { get; set; }
    public string? BrandName { get; set; }
    public string? SiteCode { get; set; }
}

public class AmoebaBatchClassifyRequest
{
    public List<AmoebaClassifyItem> Items { get; set; } = new();
}

public class AmoebaClassifyItem
{
    public long EntryId { get; set; }
    public long PLItemId { get; set; }
}

// 映射规则 DTO
public class AmoebaMappingRuleDto
{
    public long Id { get; set; }
    public long UnitId { get; set; }
    public string? UnitName { get; set; }
    public int DataSourceType { get; set; }
    public string? SiteCode { get; set; }
    public string? BrandCode { get; set; }
    public string? Direction { get; set; }
    public string? AuxField { get; set; }
    public string? AuxValue { get; set; }
    public int Priority { get; set; }
    public string? Remark { get; set; }
}

public class CreateMappingRuleRequest
{
    public long UnitId { get; set; }
    public int DataSourceType { get; set; }
    public string? SiteCode { get; set; }
    public string? BrandCode { get; set; }
    public string? Direction { get; set; }
    public string? AuxField { get; set; }
    public string? AuxValue { get; set; }
    public int Priority { get; set; }
    public string? Remark { get; set; }
}

// ===== 损益项明细钻取 =====
public class AmoebaPLItemDetailRequest
{
    public long TemplateId { get; set; }
    public long AccountSetId { get; set; }
    public long PLItemId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<long>? UnitIds { get; set; }
}

// ===== 出港收入按业务对象下钻 =====
public class AmoebaBillingDrillDownResponse
{
    public string UnitName { get; set; } = "";
    public string DateRange { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public List<ClientTypeGroup> Groups { get; set; } = new();
}

public class ClientTypeGroup
{
    public string TypeCode { get; set; } = "";   // KH/DL/WD/YW/CB/YZ
    public string TypeName { get; set; } = "";   // 直接客户/代理商/快递网点...
    public decimal SubTotal { get; set; }
    public List<ClientSummary> Clients { get; set; } = new();
}

public class ClientSummary
{
    public string ClientId { get; set; } = "";
    public string ClientName { get; set; } = "";
    public decimal Amount { get; set; }
}

public class AmoebaPLItemDetailResponse
{
    public List<AccountSummaryItem> Accounts { get; set; } = new();
    public decimal TotalAmount { get; set; }
}

public class AccountSummaryItem
{
    public long AccountId { get; set; }
    public string AccountCode { get; set; } = "";
    public string AccountName { get; set; } = "";
    public decimal Amount { get; set; }
    public List<VoucherEntryDetail> Entries { get; set; } = new();
}

public class VoucherEntryDetail
{
    public long VoucherId { get; set; }
    public long EntryId { get; set; }
    public string VoucherNumber { get; set; } = "";
    public DateTime Date { get; set; }
    public string Summary { get; set; } = "";
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public decimal Amount { get; set; }
}

// ===== 折旧项下钻 =====
public class DepreciationDrillDownRequest
{
    public long PlItemId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public long AccountSetId { get; set; }
}

public class DepreciationDrillDownResponse
{
    public decimal TotalAmount { get; set; }
    public List<AssetDepreciationDetail> Assets { get; set; } = new();
}

public class AssetDepreciationDetail
{
    public long AssetCardId { get; set; }
    public string AssetCode { get; set; } = "";
    public string AssetName { get; set; } = "";
    public decimal OriginalValue { get; set; }
    public decimal MonthlyDepreciation { get; set; }
    public decimal PeriodDepreciation { get; set; }
    public string? Department { get; set; }
}

// ===== 手工填报数据 =====
public class SaveManualDataRequest
{
    public long TemplateId { get; set; }
    public long OrgId { get; set; }
    public string Period { get; set; } = "";
    public List<ManualDataItem> Items { get; set; } = new();
}

public class ManualDataItem
{
    public long PLItemId { get; set; }
    public decimal Amount { get; set; }
    public decimal? PerUnitValue { get; set; }
}

public class ManualDataDto
{
    public long Id { get; set; }
    public long? PLItemId { get; set; }
    public decimal Amount { get; set; }
    public decimal? PerUnitValue { get; set; }
    // 暂估数据相关字段（FDataType="estimate" 时使用）
    public long TemplateId { get; set; }
    public long OrgId { get; set; }
    public string Period { get; set; } = "";
    public string DataType { get; set; } = "manual"; // manual / estimate
    public string? AccountCode { get; set; }
    public string? AuxiliaryJson { get; set; }
}

// ===== 多期对比报表（功能分区制） =====
public class AmoebaMultiPeriodRequest
{
    public long TemplateId { get; set; }
    public long OrgId { get; set; }
    public long AccountSetId { get; set; }
    public string MainPeriod { get; set; } = "";     // 主期间；格式随 Granularity：day=YYYYMMDD/week=YYYY-Www/month=YYYYMM/quarter=YYYYQn/year=YYYY
    public bool IncludeYoy { get; set; }              // 是否包含同比（日/周无同比，自动忽略）
    public AmoebaReportScope? Scope { get; set; }     // [方案B 批次4] L1 请求级作用域过滤;null=全口径
    public string? Granularity { get; set; }          // [批次5-S3] 周期粒度 day/week/month/quarter/year；null/空=month（前端不传则全兼容）
}

/// <summary>
/// [方案B 批次4] 报表作用域:维度内 OR、跨维度 AND;空集合=该维度不约束。
/// 子报表(任一维度被约束)的公共费走分摊注入而非科目匹配(批次5)。
/// </summary>
public class AmoebaReportScope
{
    public List<string>? Outlets { get; set; }      // 网点编号 → dp.SiteCode
    public List<string>? Projects { get; set; }     // 项目编码 → dp.AuxValues["project"]
    public List<string>? Directions { get; set; }   // OUT/IN/CMB → dp.AuxValues["business_direction"]
    public List<long>? Units { get; set; }           // 经营单元ID → dp.BusinessUnitId(可扩展)
    public List<string>? Brands { get; set; }        // 品牌编码 → dp.BrandCode(可扩展)

    /// <summary>是否子报表(任一维度被约束)。</summary>
    public bool IsSubReport =>
        (Outlets?.Count > 0) || (Projects?.Count > 0) || (Directions?.Count > 0)
        || (Units?.Count > 0) || (Brands?.Count > 0);
}

public class AmoebaMultiPeriodResponse
{
    public List<TabNodeDto> TabNodes { get; set; } = new();
    public List<GlobalSummaryDto> GlobalSummaries { get; set; } = new();
    public List<SectionData> Sections { get; set; } = new();
    public MultiPeriodSummary Summary { get; set; } = new();
    public List<string> PeriodLabels { get; set; } = new();
    public List<string>? UnmatchedWarnings { get; set; }
    public List<SectionData>? IndicatorSections { get; set; }  // 全局指标分区数据（不归属任何Tab）
}

/// <summary>
/// Tab节点（来自模板中 FParentId==0 且 FNodeRole=="group" 的节点）
/// </summary>
public class TabNodeDto
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public int Sort { get; set; }
    public decimal? FormulaValue { get; set; }
}

/// <summary>
/// 全局汇总行（来自模板中 FParentId==0 且 FNodeRole=="formula" 的节点）
/// </summary>
public class GlobalSummaryDto
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string? Formula { get; set; }
    public decimal? Value { get; set; }
    public List<PeriodValue> PeriodValues { get; set; } = new();
    public string? Unit { get; set; }
}

public class SectionData
{
    public string SectionName { get; set; } = "";
    /// <summary>
    /// Tab归属（depth=0 group 祖先的 ID）
    /// </summary>
    public long TabAncestorId { get; set; }
    public List<MultiPeriodPLItemData> Items { get; set; } = new();
    public List<PeriodValue>? SectionTotals { get; set; }
}

public class MultiPeriodPLItemData
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string? Unit { get; set; }
    public string? DataSourceRemark { get; set; }
    public string? CalculationLogic { get; set; }
    public bool IsManualEntry { get; set; }
    public string NodeRole { get; set; } = "";
    public int Depth { get; set; }
    public List<PeriodValue> PeriodValues { get; set; } = new();
    public decimal? MomChange { get; set; }
    public decimal? YoyChange { get; set; }
    public bool CanDrillDown { get; set; }
    public string? ItemCategory { get; set; }    // 项目类别
    public string? ValueSource { get; set; }     // 值来源
    public int? DecimalPlaces { get; set; }      // 小数位数：null=按单位自动判断(默认2位), 1~4
}

public class PeriodValue
{
    public string PeriodLabel { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal? PerUnitValue { get; set; }
}

public class MultiPeriodSummary
{
    public List<PeriodValue> MarginTotals { get; set; } = new();
    public int CurrentPeriodTickets { get; set; }
}
