using global::System.Text.Json.Serialization;

namespace STOTOP.Module.Finance.Dtos;

public class AccountBalanceDto
{
    [JsonPropertyName("accountId")]
    public long AccountId { get; set; }
    
    [JsonPropertyName("accountCode")]
    public string AccountCode { get; set; } = string.Empty;
    
    [JsonPropertyName("accountName")]
    public string AccountName { get; set; } = string.Empty;
    
    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;
    
    [JsonPropertyName("level")]
    public int Level { get; set; }
    
    [JsonPropertyName("beginDebit")]
    public decimal BeginDebit { get; set; }
    
    [JsonPropertyName("beginCredit")]
    public decimal BeginCredit { get; set; }
    
    [JsonPropertyName("currentDebit")]
    public decimal CurrentDebit { get; set; }
    
    [JsonPropertyName("currentCredit")]
    public decimal CurrentCredit { get; set; }
    
    [JsonPropertyName("endDebit")]
    public decimal EndDebit { get; set; }
    
    [JsonPropertyName("endCredit")]
    public decimal EndCredit { get; set; }
}

public class AuxiliaryBalanceDto
{
    public long AccountId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string AuxiliaryJson { get; set; } = string.Empty;
    public string AuxiliaryInfo { get; set; } = string.Empty;
    public decimal OpeningDebit { get; set; }
    public decimal OpeningCredit { get; set; }
    public decimal PeriodDebit { get; set; }
    public decimal PeriodCredit { get; set; }
    public decimal ClosingDebit { get; set; }
    public decimal ClosingCredit { get; set; }
    public decimal BeginDebit { get; set; }
    public decimal BeginCredit { get; set; }
    public decimal CurrentDebit { get; set; }
    public decimal CurrentCredit { get; set; }
    public decimal EndDebit { get; set; }
    public decimal EndCredit { get; set; }
}

public class AssetBalanceDto
{
    public long AssetId { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public long CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public long? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public decimal OriginalValue { get; set; }
    public decimal AccumulatedDepreciation { get; set; }
    public decimal NetValue { get; set; }
    public int AssetCount { get; set; }
    public decimal OriginalValueTotal { get; set; }
    public decimal AccumulatedDepreciationTotal { get; set; }
    public decimal NetValueTotal { get; set; }
}

public class ProfitStatementDto
{
    public string ItemName { get; set; } = string.Empty;
    public int RowIndex { get; set; }
    
    // 本年累计
    public decimal YearAccumulatedAmount { get; set; }
    public decimal YearRevenueRatio { get; set; }
    
    // 本期
    public decimal CurrentAmount { get; set; }
    public decimal CurrentRevenueRatio { get; set; }
    
    // 占比差值
    public decimal RatioDifference { get; set; }
}

public class BalanceSheetDto
{
    public string ItemName { get; set; } = string.Empty;
    public int RowIndex { get; set; }
    public string? LineNo { get; set; }
    public decimal EndAmount { get; set; }
    public decimal BeginAmount { get; set; }
    public string Category { get; set; } = string.Empty; // 资产/负债/权益
}

public class CashFlowDto
{
    public string Id { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public int RowIndex { get; set; }
    public int Level { get; set; } = 1;
    public bool IsTotal { get; set; }
    public decimal CurrentAmount { get; set; }
    public decimal PreviousAmount { get; set; }
    public decimal Amount { get; set; }
    public string Category { get; set; } = string.Empty; // 经营/投资/筹资
}

public class TaxPayableDto
{
    public string TaxName { get; set; } = string.Empty;
    public decimal OpeningBalance { get; set; }
    public decimal PeriodIncrease { get; set; }
    public decimal PeriodDecrease { get; set; }
    public decimal ClosingBalance { get; set; }
    public decimal BeginAmount { get; set; }
    public decimal CurrentPayable { get; set; }
    public decimal CurrentPaid { get; set; }
    public decimal EndAmount { get; set; }
}

public class AmoebaPLSummaryDto
{
    public long ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string NodeRole { get; set; } = string.Empty;
    public int Depth { get; set; }
    
    // 参考金额（本年累计）
    public decimal YearAccumulatedAmount { get; set; }
    public decimal YearRevenueRatio { get; set; }
    
    // 本期金额
    public decimal CurrentAmount { get; set; }
    public decimal CurrentRevenueRatio { get; set; }
    
    // 占比差值
    public decimal RatioDifference { get; set; }
    
    public List<AmoebaPLSummaryDto> Children { get; set; } = new();
}

public class AmoebaPLChartDto
{
    // 环形图数据
    public ChartDataItem[] PieChartData { get; set; } = Array.Empty<ChartDataItem>();
    
    // 12月走势数据
    public TrendDataItem[] TrendData { get; set; } = Array.Empty<TrendDataItem>();
    
    // 比较图数据
    public CompareDataItem[] CompareData { get; set; } = Array.Empty<CompareDataItem>();
}

public class ChartDataItem
{
    public string Name { get; set; } = string.Empty;
    public decimal Value { get; set; }
}

public class TrendDataItem
{
    public int Month { get; set; }
    public decimal Income { get; set; }
    public decimal Expense { get; set; }
    public decimal Profit { get; set; }
}

public class CompareDataItem
{
    public string Name { get; set; } = string.Empty;
    public decimal CurrentValue { get; set; }
    public decimal PreviousValue { get; set; }
    public decimal GrowthRate { get; set; }
}

public class AmoebaPLReportDto
{
    public AmoebaPLChartDto ChartData { get; set; } = new();
    public List<AmoebaPLSummaryDto> SummaryTable { get; set; } = new();
}

/// <summary>
/// 小企业利润表DTO（34行格式）
/// </summary>
public class SmallEnterpriseProfitStatementDto
{
    public string ItemName { get; set; } = string.Empty;
    public int RowIndex { get; set; }
    /// <summary>
    /// 本年累计金额
    /// </summary>
    public decimal YearAccumulatedAmount { get; set; }
    /// <summary>
    /// 本期金额
    /// </summary>
    public decimal CurrentAmount { get; set; }
    /// <summary>
    /// 是否为主标题行（如“一、营业收入”）
    /// </summary>
    public bool IsMainTitle { get; set; }
    /// <summary>
    /// 是否为子标题行（如“减：营业成本”）
    /// </summary>
    public bool IsSubTitle { get; set; }
    /// <summary>
    /// 是否为缩进行（如“其中：消费税”）
    /// </summary>
    public bool IsIndent { get; set; }
    /// <summary>
    /// 缩进级别
    /// </summary>
    public int IndentLevel { get; set; }
}

/// <summary>
/// 科目明细账DTO
/// </summary>
public class AccountDetailDto
{
    [JsonPropertyName("date")]
    public string Date { get; set; } = string.Empty;
    
    [JsonPropertyName("voucherNo")]
    public string VoucherNo { get; set; } = string.Empty;
    
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;
    
    [JsonPropertyName("debitAmount")]
    public decimal DebitAmount { get; set; }
    
    [JsonPropertyName("creditAmount")]
    public decimal CreditAmount { get; set; }
    
    [JsonPropertyName("balance")]
    public decimal Balance { get; set; }
    
    [JsonPropertyName("direction")]
    public string Direction { get; set; } = string.Empty; // 借 或 贷
    
    [JsonPropertyName("isOpeningBalance")]
    public bool IsOpeningBalance { get; set; }
}

/// <summary>
/// 科目明细账查询结果
/// </summary>
public class AccountDetailResultDto
{
    [JsonPropertyName("accountCode")]
    public string AccountCode { get; set; } = string.Empty;
    
    [JsonPropertyName("accountName")]
    public string AccountName { get; set; } = string.Empty;
    
    [JsonPropertyName("items")]
    public List<AccountDetailDto> Items { get; set; } = new();
}

// 钻取明细DTO
public class DrillDownItemDto
{
    public long VoucherId { get; set; }
    public string VoucherNo { get; set; } = "";
    public DateTime VoucherDate { get; set; }
    public string Summary { get; set; } = "";
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public string AccountCode { get; set; } = "";
    public string AccountName { get; set; } = "";
}

// 趋势数据DTO
public class ProfitTrendDto
{
    public int Month { get; set; }
    public decimal Revenue { get; set; }
    public decimal Cost { get; set; }
    public decimal Expense { get; set; }
    public decimal Profit { get; set; }
}

// 构成数据DTO（饼图）
public class CompositionItemDto
{
    public string Name { get; set; } = "";
    public decimal Value { get; set; }
    public decimal Percentage { get; set; }
}

// 同比环比DTO
public class ComparisonDto
{
    public string ItemName { get; set; } = "";
    public decimal CurrentValue { get; set; }
    public decimal PreviousValue { get; set; }
    public decimal ChangeAmount { get; set; }
    public decimal ChangeRate { get; set; } // 百分比
}
