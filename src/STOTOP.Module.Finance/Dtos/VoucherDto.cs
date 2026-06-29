using STOTOP.Core.Models;

namespace STOTOP.Module.Finance.Dtos;

public class VoucherDto
{
    public long Id { get; set; }
    public string VoucherWord { get; set; } = string.Empty;
    public int VoucherNo { get; set; }
    public DateTime Date { get; set; }
    public long PeriodId { get; set; }
    public string PeriodName { get; set; } = string.Empty;
    public int AttachmentCount { get; set; }
    public string Creator { get; set; } = string.Empty;
    public string? Auditor { get; set; }
    public string? Modifier { get; set; }
    public int Status { get; set; }
    public string? Source { get; set; }
    public string? Remark { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public List<VoucherEntryDto> Entries { get; set; } = new();
}

public class VoucherListDto
{
    public long Id { get; set; }
    public string VoucherWord { get; set; } = string.Empty;
    public int VoucherNo { get; set; }
    public DateTime Date { get; set; }
    public long PeriodId { get; set; }
    public string PeriodName { get; set; } = string.Empty;
    public string Creator { get; set; } = string.Empty;
    public string? Auditor { get; set; }
    public int Status { get; set; }
    public decimal TotalAmount { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string? Remark { get; set; }
    public List<VoucherEntryDto> Entries { get; set; } = new();
}

public class VoucherEntryDto
{
    public long Id { get; set; }
    public int LineNo { get; set; }
    public string Summary { get; set; } = string.Empty;
    public long AccountId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string? AuxiliaryJson { get; set; }
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
}

public class CreateVoucherRequest
{
    public string VoucherWord { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public long PeriodId { get; set; }
    public int AttachmentCount { get; set; }
    public string? Remark { get; set; }
    public string? Source { get; set; }         // 来源标记（如 "数据导入"）
    public string? DataScopeId { get; set; }    // 数据血缘标记（批次ID等）
    public List<CreateVoucherEntryRequest> Entries { get; set; } = new();
}

public class CreateVoucherEntryRequest
{
    public int LineNo { get; set; }
    public string Summary { get; set; } = string.Empty;
    public long AccountId { get; set; }
    public string? AuxiliaryJson { get; set; }
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
}

public class VoucherQueryRequest : PagedRequest
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? Date { get; set; }
    public int? Status { get; set; }
    public string? VoucherWord { get; set; }
    public long? PeriodId { get; set; }
    public long? StartPeriodId { get; set; }
    public long? EndPeriodId { get; set; }
    /// <summary>按来源筛选（如"费用支出"）</summary>
    public string? Source { get; set; }
    /// <summary>搜索字段：summary/account/voucherNumber/remark</summary>
    public string? SearchField { get; set; }
}

public class VoucherPagedResult
{
    public List<VoucherListDto> Items { get; set; } = new();
    public int Total { get; set; }
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    /// <summary>当前查询条件下（不含状态过滤）的凭证总数</summary>
    public int TotalAllCount { get; set; }
    /// <summary>当前查询条件下待审核(status=1)的凭证数量</summary>
    public int PendingCount { get; set; }
    /// <summary>当前查询条件下待补录凭证数量（来源=费用支出 且 备注含[待补录] 且 状态=0）</summary>
    public int PendingRecordCount { get; set; }
}

public class VoucherAuditRequest
{
    public string Auditor { get; set; } = string.Empty;
}
