using global::System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;

namespace STOTOP.Module.Finance.Dtos;

public class JournalQueryRequest
{
    public long AccountSetId { get; set; }
    public string? QueryMode { get; set; }       // "period" | "date" | "period-range" | "date-range"
    public int? Year { get; set; }
    public int? Month { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? StartYear { get; set; }
    public int? StartMonth { get; set; }
    public int? EndYear { get; set; }
    public int? EndMonth { get; set; }
    public string? AccountCode { get; set; }
    public string? Category { get; set; }
    public string? SearchField { get; set; }
    public string? SearchText { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "页码必须大于0")]
    public int Page { get; set; } = 1;

    [Range(1, 1000, ErrorMessage = "每页数量必须在1-1000之间")]
    public int PageSize { get; set; } = 100;
}

public class JournalEntryDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }
    [JsonPropertyName("voucherId")]
    public long VoucherId { get; set; }
    [JsonPropertyName("date")]
    public DateTime Date { get; set; }
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;
    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;
    [JsonPropertyName("accountCode")]
    public string AccountCode { get; set; } = string.Empty;
    [JsonPropertyName("accountName")]
    public string AccountName { get; set; } = string.Empty;
    [JsonPropertyName("debitAmount")]
    public decimal DebitAmount { get; set; }
    [JsonPropertyName("creditAmount")]
    public decimal CreditAmount { get; set; }
    [JsonPropertyName("balance")]
    public decimal Balance { get; set; }
    [JsonPropertyName("direction")]
    public string Direction { get; set; } = string.Empty;
    [JsonPropertyName("voucherNo")]
    public string VoucherNo { get; set; } = string.Empty;
    [JsonPropertyName("voucherStatus")]
    public int VoucherStatus { get; set; }
    [JsonPropertyName("isInitialBalance")]
    public bool IsInitialBalance { get; set; }
}

public class JournalPagedResult
{
    [JsonPropertyName("items")]
    public List<JournalEntryDto> Items { get; set; } = new();
    [JsonPropertyName("total")]
    public int Total { get; set; }
    [JsonPropertyName("page")]
    public int Page { get; set; }
    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }
}

public class JournalAdjustRequest
{
    public DateTime Date { get; set; }
    public string Summary { get; set; } = string.Empty;
    public long AccountId { get; set; }
    public string Direction { get; set; } = "credit"; // "debit" | "credit"
    public decimal Amount { get; set; }
    public long AccountSetId { get; set; }
    public bool SaveAsDraft { get; set; }
}

public class JournalGenerateVoucherRequest
{
    public List<long> EntryIds { get; set; } = new();
    public long AccountSetId { get; set; }
}

public class JournalCreateRequest
{
    /// <summary>"income" | "expense" | "transfer"（收入/支出/收支）</summary>
    public string Type { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string? Category { get; set; }
    public string Summary { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    /// <summary>主账户科目ID（收入/支出时为现金银行账户；收支-内部转账时为源账户）</summary>
    public long AccountId { get; set; }
    public List<long>? MultiAccountIds { get; set; }
    public int AttachmentCount { get; set; }
    public long AccountSetId { get; set; }
    public bool SaveAsDraft { get; set; }

    // ── 收支（transfer）子模式 ──────────────────────────────
    /// <summary>"payment" | "internal-transfer"（收付款 / 内部转账）</summary>
    public string? SubType { get; set; }
    /// <summary>"receivable" | "payable"（应收核销 / 应付核销）</summary>
    public string? ReconcileDirection { get; set; }
    /// <summary>核销账户ID（1122应收 或 2202应付 子科目）</summary>
    public long? ReconcileAccountId { get; set; }
    /// <summary>核销金额</summary>
    public decimal? ReconcileAmount { get; set; }
    /// <summary>现金银行账户ID（1001/1002子科目）</summary>
    public long? CashBankAccountId { get; set; }
    /// <summary>内部转账目标账户ID</summary>
    public long? TransferToAccountId { get; set; }

    /// <summary>多账户模式：每个账户及对应金额（有值时优先使用，覆盖 AccountId + Amount 的单账户模式）</summary>
    public List<JournalAccountItem>? AccountItems { get; set; }
}

/// <summary>多账户模式中的账户明细项</summary>
public class JournalAccountItem
{
    /// <summary>科目ID</summary>
    public long AccountId { get; set; }
    /// <summary>该账户金额</summary>
    public decimal Amount { get; set; }
}
