namespace STOTOP.Module.CardFlow.Models;

/// <summary>
/// CardVoucherAutoPlugin 凭证生成配置（持久化在 CfStageDefinition.FAutoPluginConfigJson）
/// </summary>
public class CardVoucherConfig
{
    /// <summary>凭证类型，例如 "expense" / "payment"</summary>
    public string? VoucherType { get; set; }

    /// <summary>凭证字（如 "记"）</summary>
    public string? VoucherWord { get; set; } = "记";

    /// <summary>记账日期所在的卡片字段 key</summary>
    public string? DateField { get; set; }

    /// <summary>摘要模板，支持 {fieldName} 占位符（如 "报销-{applicantName}-{expenseSummary}"）</summary>
    public string? SummaryTemplate { get; set; }

    /// <summary>分录配置</summary>
    public List<VoucherLineConfig> Lines { get; set; } = new();

    /// <summary>凭证 ID 回写到 cardData 的字段 key（如 "voucher1Ref" / "voucher2Ref"）</summary>
    public string? WriteBackField { get; set; }
}

public class VoucherLineConfig
{
    /// <summary>"借" / "贷"</summary>
    public string Direction { get; set; } = "借";

    /// <summary>科目来源："cardField" / "fixed"</summary>
    public string AccountSource { get; set; } = "cardField";

    /// <summary>accountSource=cardField 时，卡片中的字段 key（值可为科目编码或科目ID）</summary>
    public string? AccountField { get; set; }

    /// <summary>accountSource=fixed 时的对方科目配置</summary>
    public CounterpartAccountConfig? CounterpartAccount { get; set; }

    /// <summary>金额字段 key</summary>
    public string AmountField { get; set; } = string.Empty;

    /// <summary>"cardField" / "auto" / null</summary>
    public string? AuxiliarySource { get; set; }

    /// <summary>auxiliarySource=cardField 时，参与的卡片字段 keys</summary>
    public List<string>? AuxiliaryFields { get; set; }

    /// <summary>auxiliarySource=auto 时的解析配置</summary>
    public AuxiliaryAutoConfig? AuxiliaryConfig { get; set; }
}

public class CounterpartAccountConfig
{
    public long? AccountId { get; set; }
    public string? AccountCode { get; set; }
}

public class AuxiliaryAutoConfig
{
    /// <summary>"employee" / "supplier" / "department"</summary>
    public string AuxType { get; set; } = string.Empty;

    /// <summary>卡片中的匹配源字段</summary>
    public string SourceField { get; set; } = string.Empty;

    /// <summary>"code" / "name"</summary>
    public string MatchBy { get; set; } = "code";

    public string? FallbackField { get; set; }
    public string? FallbackMatchBy { get; set; }
}
