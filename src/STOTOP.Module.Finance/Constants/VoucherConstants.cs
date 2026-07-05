namespace STOTOP.Module.Finance.Constants;

/// <summary>凭证字</summary>
public static class VoucherWord
{
    /// <summary>记账凭证</summary>
    public const string Ji = "记";

    /// <summary>收款凭证</summary>
    public const string Shou = "收";

    /// <summary>付款凭证</summary>
    public const string Fu = "付";

    /// <summary>转账凭证</summary>
    public const string Zhuan = "转";

    /// <summary>凭证字全集（单一真源；账套未配置启用子集时的默认回退）</summary>
    public static readonly string[] AllWords = { Ji, Shou, Fu, Zhuan };
}

/// <summary>凭证状态</summary>
public enum VoucherStatus
{
    /// <summary>作废（红冲/撤销后置此态，不参与余额与报表）</summary>
    Voided = -1,

    /// <summary>草稿</summary>
    Draft = 0,

    /// <summary>待审核</summary>
    Pending = 1,

    /// <summary>已审核</summary>
    Audited = 2,

    /// <summary>已锁定（期间结账后置此态；仍为已入账凭证，参与余额与报表，反结账恢复为 Audited）</summary>
    Locked = 3
}

/// <summary>凭证来源标识（FSource 单一真源）</summary>
public static class VoucherSource
{
    /// <summary>期末损益结转 / 年度利润结转（系统生成）。P&amp;L 报表取数须排除此来源，否则损益被结转分录冲平为零。</summary>
    public const string SystemClosing = "system:closing";
}
