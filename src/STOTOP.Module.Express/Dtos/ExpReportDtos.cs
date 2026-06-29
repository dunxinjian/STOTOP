namespace STOTOP.Module.Express.Dtos;

// ==================== 通用查询请求 ====================

/// <summary>
/// 报表通用查询请求
/// </summary>
public class ReportQueryRequest
{
    /// <summary>开始日期</summary>
    public DateTime? DateFrom { get; set; }
    /// <summary>结束日期</summary>
    public DateTime? DateTo { get; set; }
    /// <summary>品牌编码</summary>
    public string? BrandCode { get; set; }
    /// <summary>业务对象ID</summary>
    public string? ClientId { get; set; }
    /// <summary>省份ID</summary>
    public int? ProvinceId { get; set; }
}

// ==================== 流量流向 ====================

/// <summary>
/// 流量流向分析
/// </summary>
public class FlowAnalysisDto
{
    /// <summary>省份名称</summary>
    public string Province { get; set; } = string.Empty;
    /// <summary>运单数</summary>
    public int WaybillCount { get; set; }
    /// <summary>占比(%)</summary>
    public decimal Ratio { get; set; }
    /// <summary>总重量(kg)</summary>
    public decimal TotalWeight { get; set; }
    /// <summary>平均重量</summary>
    public decimal AvgWeight { get; set; }
    /// <summary>总应收金额</summary>
    public decimal TotalCharge { get; set; }
    /// <summary>单票均价</summary>
    public decimal AvgPrice { get; set; }
}

/// <summary>
/// 流量趋势
/// </summary>
public class FlowTrendDto
{
    /// <summary>日期</summary>
    public string Date { get; set; } = string.Empty;
    /// <summary>运单数</summary>
    public int WaybillCount { get; set; }
    /// <summary>总应收金额</summary>
    public decimal TotalCharge { get; set; }
}

// ==================== 重量段 ====================

/// <summary>
/// 重量段分布
/// </summary>
public class WeightSegmentReportDto
{
    /// <summary>段名称</summary>
    public string SegmentName { get; set; } = string.Empty;
    /// <summary>运单数</summary>
    public int WaybillCount { get; set; }
    /// <summary>占比(%)</summary>
    public decimal Ratio { get; set; }
    /// <summary>总重量</summary>
    public decimal TotalWeight { get; set; }
    /// <summary>总应收金额</summary>
    public decimal TotalCharge { get; set; }
    /// <summary>单票均价</summary>
    public decimal AvgPrice { get; set; }
    /// <summary>单公斤均价</summary>
    public decimal AvgPricePerKg { get; set; }
}

/// <summary>
/// 均重趋势
/// </summary>
public class WeightTrendDto
{
    /// <summary>日期</summary>
    public string Date { get; set; } = string.Empty;
    /// <summary>平均重量</summary>
    public decimal AvgWeight { get; set; }
}

// ==================== 毛利 ====================

/// <summary>
/// 按业务对象毛利
/// </summary>
public class ProfitByClientDto
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public int ClientType { get; set; }
    public int WaybillCount { get; set; }
    public decimal TotalWeight { get; set; }
    public decimal TotalCharge { get; set; }
    public decimal TotalCost { get; set; }
    public decimal Profit { get; set; }
    /// <summary>毛利率(%)；应收≤0 时无意义，返回 null</summary>
    public decimal? ProfitRate { get; set; }
    public decimal AvgPrice { get; set; }
    public decimal AvgProfit { get; set; }
    /// <summary>应收≤0 的运单数（未计价/零应收预警）</summary>
    public int ZeroChargeCount { get; set; }
}

/// <summary>
/// 按店铺毛利
/// </summary>
public class ProfitByShopDto
{
    public string ShopName { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public int WaybillCount { get; set; }
    public decimal TotalWeight { get; set; }
    public decimal TotalCharge { get; set; }
    public decimal TotalCost { get; set; }
    public decimal Profit { get; set; }
    /// <summary>毛利率(%)；应收≤0 时无意义，返回 null</summary>
    public decimal? ProfitRate { get; set; }
}

/// <summary>
/// 毛利趋势
/// </summary>
public class ProfitTrendDto
{
    public string Date { get; set; } = string.Empty;
    public decimal TotalCharge { get; set; }
    public decimal TotalCost { get; set; }
    public decimal Profit { get; set; }
    /// <summary>毛利率(%)；应收≤0 时无意义，返回 null</summary>
    public decimal? ProfitRate { get; set; }
}

/// <summary>
/// 按重量段毛利
/// </summary>
public class ProfitByWeightSegmentDto
{
    /// <summary>段名（如 0-0.5kg；6=无重量）</summary>
    public string WeightSegment { get; set; } = string.Empty;
    /// <summary>排序索引 0-6</summary>
    public int SegmentOrder { get; set; }
    public int WaybillCount { get; set; }
    public decimal TotalWeight { get; set; }
    public decimal TotalCharge { get; set; }
    public decimal TotalCost { get; set; }
    public decimal Profit { get; set; }
    /// <summary>毛利率(%)；应收≤0 时无意义，返回 null</summary>
    public decimal? ProfitRate { get; set; }
    /// <summary>单票均价（应收）</summary>
    public decimal AvgCharge { get; set; }
    /// <summary>单票均成本</summary>
    public decimal AvgCost { get; set; }
    /// <summary>单票均利</summary>
    public decimal AvgProfit { get; set; }
}

// ==================== 综合看板 ====================

/// <summary>
/// 综合看板
/// </summary>
public class DashboardDto
{
    /// <summary>今日单量</summary>
    public int TodayWaybills { get; set; }
    /// <summary>本月单量</summary>
    public int MonthWaybills { get; set; }
    /// <summary>本月收入</summary>
    public decimal MonthRevenue { get; set; }
    /// <summary>本月成本</summary>
    public decimal MonthCost { get; set; }
    /// <summary>本月毛利</summary>
    public decimal MonthProfit { get; set; }
    /// <summary>运单数环比变化率(%)</summary>
    public decimal? MonthWaybillsChange { get; set; }
    /// <summary>收入环比变化率(%)</summary>
    public decimal? MonthRevenueChange { get; set; }
    /// <summary>成本环比变化率(%)</summary>
    public decimal? MonthCostChange { get; set; }
    /// <summary>毛利环比变化率(%)</summary>
    public decimal? MonthProfitChange { get; set; }
    /// <summary>日趋势</summary>
    public List<DailyTrendItem> DailyTrend { get; set; } = new();
    /// <summary>品牌分布</summary>
    public List<BrandDistributionItem> BrandDistribution { get; set; } = new();
    /// <summary>TOP客户</summary>
    public List<TopClientItem> TopClients { get; set; } = new();
    /// <summary>异常预警</summary>
    public List<AlertItem> Alerts { get; set; } = new();
}

public class DailyTrendItem
{
    public string Date { get; set; } = string.Empty;
    public int WaybillCount { get; set; }
    public decimal Revenue { get; set; }
    public decimal Cost { get; set; }
    public decimal Profit { get; set; }
}

public class BrandDistributionItem
{
    public string BrandCode { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
    public int WaybillCount { get; set; }
    public decimal Ratio { get; set; }
}

public class TopClientItem
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public int WaybillCount { get; set; }
    public decimal TotalCharge { get; set; }
}

public class AlertItem
{
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int Count { get; set; }
}

// ==================== 多角色毛利 ====================

/// <summary>
/// 中间人视角毛利
/// </summary>
public class ProfitByIntermediaryDto
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    /// <summary>类型 2=业务代理、5=承包区</summary>
    public int ClientType { get; set; }
    public int ChainLevel { get; set; }
    public int WaybillCount { get; set; }
    public decimal TotalWeight { get; set; }
    /// <summary>向下收入（本级应收）</summary>
    public decimal DownstreamRevenue { get; set; }
    /// <summary>向上成本（上级应收 or 网点成本）</summary>
    public decimal UpstreamCost { get; set; }
    public decimal Profit { get; set; }
    /// <summary>毛利率(%)；应收≤0 时无意义，返回 null</summary>
    public decimal? ProfitRate { get; set; }
    /// <summary>单票均利</summary>
    public decimal AvgProfit { get; set; }
}

/// <summary>
/// 业务员/提成视角毛利
/// </summary>
public class ProfitBySalesmanDto
{
    public string SalesmanId { get; set; } = string.Empty;
    public string SalesmanName { get; set; } = string.Empty;
    public long? NetworkPointId { get; set; }
    public int WaybillCount { get; set; }
    public decimal TotalWeight { get; set; }
    /// <summary>提成收入</summary>
    public decimal CommissionIncome { get; set; }
    /// <summary>= CommissionIncome（无成本）</summary>
    public decimal Profit { get; set; }
    /// <summary>单票均提成</summary>
    public decimal AvgCommission { get; set; }
}

// ==================== 大区/省份流量损益 ====================

/// <summary>
/// 按大区流量损益
/// </summary>
public class ProfitByRegionDto
{
    /// <summary>大区名称（省内/一区/二区/三区/四区/五区/未知）</summary>
    public string Region { get; set; } = string.Empty;
    /// <summary>排序序号（AllRegions 索引）</summary>
    public int RegionOrder { get; set; }
    public int WaybillCount { get; set; }
    public decimal TotalWeight { get; set; }
    public decimal TotalCharge { get; set; }
    public decimal TotalCost { get; set; }
    public decimal Profit { get; set; }
    /// <summary>毛利率(%)；应收≤0 时无意义，返回 null</summary>
    public decimal? ProfitRate { get; set; }
    /// <summary>单票均重(kg)</summary>
    public decimal AvgWeight { get; set; }
    /// <summary>单票均利</summary>
    public decimal AvgProfit { get; set; }
}

/// <summary>
/// 按省份流量损益
/// </summary>
public class ProfitByProvinceDto
{
    public int ProvinceId { get; set; }
    public string ProvinceName { get; set; } = string.Empty;
    /// <summary>所属大区</summary>
    public string Region { get; set; } = string.Empty;
    public int WaybillCount { get; set; }
    public decimal TotalWeight { get; set; }
    public decimal TotalCharge { get; set; }
    public decimal TotalCost { get; set; }
    public decimal Profit { get; set; }
    /// <summary>毛利率(%)；应收≤0 时无意义，返回 null</summary>
    public decimal? ProfitRate { get; set; }
    /// <summary>单票均利</summary>
    public decimal AvgProfit { get; set; }
}

// ==================== 筛选选项 ====================

/// <summary>
/// 毛利报表筛选选项
/// </summary>
public class ProfitFilterOptionsDto
{
    public List<FilterOptionItem> Brands { get; set; } = [];
    public List<FilterOptionItem> Clients { get; set; } = [];
}

public class FilterOptionItem
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}
