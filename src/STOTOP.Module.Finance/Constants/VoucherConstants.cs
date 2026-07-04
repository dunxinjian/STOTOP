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
    /// <summary>草稿</summary>
    Draft = 0,

    /// <summary>待审核</summary>
    Pending = 1,

    /// <summary>已审核</summary>
    Audited = 2
}
