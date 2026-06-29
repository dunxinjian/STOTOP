namespace STOTOP.Core.Interfaces;

/// <summary>
/// 凭证服务抽象接口：CardFlow 模块通过此接口生成/红冲凭证，避免直接依赖 Finance 模块
/// 实现类位于 STOTOP.Module.Finance（CardFlow ↔ Finance 解耦落地）
/// </summary>
public interface IVoucherService
{
    /// <summary>创建凭证（含分录），返回新凭证 ID。
    /// 实现端可在 FPeriodId=0 时按 FDate+FAccountSetId 自动解析期间；
    /// 分录中 FAccountId=0 而 FAccountCode 非空时，按编码+账套解析科目。
    /// </summary>
    Task<long> CreateAsync(VoucherCreateDto voucher);

    /// <summary>红冲凭证（金额取反生成新凭证，并标记原凭证已撤销）</summary>
    Task CreateReversalAsync(long voucherId);

    /// <summary>查询组织默认账套 ID（用于 CardFlow 等模块在未指定账套时回退）</summary>
    Task<long?> GetDefaultAccountSetIdAsync(long orgId);
}

/// <summary>凭证创建 DTO（参考 FinVoucher 结构）</summary>
public class VoucherCreateDto
{
    public string FVoucherWord { get; set; } = "记";
    public DateTime FDate { get; set; }
    public long FPeriodId { get; set; }
    public long FAccountSetId { get; set; }
    public long FOrgId { get; set; }
    public string FCreator { get; set; } = string.Empty;
    public string? FRemark { get; set; }
    public string? FSource { get; set; }
    /// <summary>数据血缘标记（如卡片ID/批次ID）</summary>
    public string? FDataScopeId { get; set; }
    public int FAttachmentCount { get; set; }
    public List<VoucherEntryCreateDto> Entries { get; set; } = new();
}

/// <summary>凭证分录 DTO</summary>
public class VoucherEntryCreateDto
{
    public int FLineNo { get; set; }
    public string FSummary { get; set; } = string.Empty;
    public long FAccountId { get; set; }
    public string FAccountCode { get; set; } = string.Empty;
    public string FAccountName { get; set; } = string.Empty;
    public string? FAuxiliaryJson { get; set; }
    public decimal FDebitAmount { get; set; }
    public decimal FCreditAmount { get; set; }
    public string? FCurrencyCode { get; set; }
    public decimal? FExchangeRate { get; set; }
    public decimal? FOriginalAmount { get; set; }
}
