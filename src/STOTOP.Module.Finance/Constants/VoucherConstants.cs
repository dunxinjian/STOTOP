namespace STOTOP.Module.Finance.Constants;

/// <summary>凭证字</summary>
public static class VoucherWord
{
    /// <summary>记账凭证</summary>
    public const string Ji = "记";
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
