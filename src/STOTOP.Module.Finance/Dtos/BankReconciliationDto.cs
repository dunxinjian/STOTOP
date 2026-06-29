using STOTOP.Core.Models;

namespace STOTOP.Module.Finance.Dtos;

public class BankStatementDto
{
    public long Id { get; set; }
    public string? BankAccount { get; set; }
    public string? BankName { get; set; }
    public DateTime TransactionDate { get; set; }
    public string? Description { get; set; }
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public decimal Balance { get; set; }
    public string? Counterparty { get; set; }
    public string? ReferenceNo { get; set; }
    public int MatchStatus { get; set; }
    public long? MatchedVoucherId { get; set; }
    public long? ImportBatchId { get; set; }
    public DateTime CreatedTime { get; set; }
}

public class BankStatementImportRequest
{
    public string? BankAccount { get; set; }
    public string? BankName { get; set; }
    /// <summary>起始数据行号（从1开始，跳过表头）</summary>
    public int StartRow { get; set; } = 2;
    /// <summary>日期列号（从0开始）</summary>
    public int DateColumnIndex { get; set; } = 0;
    /// <summary>摘要列号</summary>
    public int DescriptionColumnIndex { get; set; } = 1;
    /// <summary>借方金额列号（收入）</summary>
    public int DebitColumnIndex { get; set; } = 2;
    /// <summary>贷方金额列号（支出）</summary>
    public int CreditColumnIndex { get; set; } = 3;
    /// <summary>余额列号</summary>
    public int BalanceColumnIndex { get; set; } = 4;
    /// <summary>对方户名列号</summary>
    public int CounterpartyColumnIndex { get; set; } = 5;
    /// <summary>参考号/流水号列号（-1表示无此列）</summary>
    public int ReferenceNoColumnIndex { get; set; } = -1;
}

public class BankStatementQueryRequest : PagedRequest
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? MatchStatus { get; set; }
    public string? BankAccount { get; set; }
}

public class ManualMatchRequest
{
    public long BankStatementId { get; set; }
    public long VoucherId { get; set; }
    public long? VoucherEntryId { get; set; }
}

public class ReconciliationReportDto
{
    /// <summary>银行流水余额</summary>
    public decimal BankBalance { get; set; }
    /// <summary>企业已收银行未收</summary>
    public decimal CompanyReceivedBankNot { get; set; }
    /// <summary>企业已付银行未付</summary>
    public decimal CompanyPaidBankNot { get; set; }
    /// <summary>银行端调节后余额</summary>
    public decimal AdjustedBankBalance { get; set; }
    /// <summary>账面余额</summary>
    public decimal BookBalance { get; set; }
    /// <summary>银行已收企业未收</summary>
    public decimal BankReceivedCompanyNot { get; set; }
    /// <summary>银行已付企业未付</summary>
    public decimal BankPaidCompanyNot { get; set; }
    /// <summary>账面端调节后余额</summary>
    public decimal AdjustedBookBalance { get; set; }
    /// <summary>未达账项明细 - 企业已收银行未收</summary>
    public List<UnmatchedVoucherDto> CompanyReceivedItems { get; set; } = new();
    /// <summary>未达账项明细 - 企业已付银行未付</summary>
    public List<UnmatchedVoucherDto> CompanyPaidItems { get; set; } = new();
    /// <summary>未达账项明细 - 银行已收企业未收</summary>
    public List<BankStatementDto> BankReceivedItems { get; set; } = new();
    /// <summary>未达账项明细 - 银行已付企业未付</summary>
    public List<BankStatementDto> BankPaidItems { get; set; } = new();
}

public class UnmatchedVoucherDto
{
    public long VoucherId { get; set; }
    public long EntryId { get; set; }
    public DateTime Date { get; set; }
    public string VoucherNo { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
}

public class BankStatementPagedResult
{
    public List<BankStatementDto> Items { get; set; } = new();
    public int Total { get; set; }
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public int MatchedCount { get; set; }
    public int UnmatchedCount { get; set; }
}
