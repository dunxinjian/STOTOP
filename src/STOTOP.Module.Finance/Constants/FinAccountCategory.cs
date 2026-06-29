namespace STOTOP.Module.Finance.Constants;

/// <summary>
/// 会计科目「损益类」判定的单一真源。
///
/// 科目种子中损益科目的 FCategory 存的是子类名（营业收入/营业成本/期间费用…），
/// 大类名「损益」并不落库；但早期数据/兼容路径可能以「损益」落库，故集合中一并纳入。
/// 取数/结账处统一用此处判定，避免再出现硬编码字符串与前后端双真源。
/// </summary>
public static class FinAccountCategory
{
    /// <summary>
    /// 损益子类集合（含大类名「损益」兼容历史脏数据）。
    /// 用数组以便 EF Core 在 Where 中翻译为 SQL IN。
    /// </summary>
    public static readonly string[] ProfitLossCategories =
    {
        "损益", "营业收入", "营业成本", "营业税金及附加", "期间费用",
        "其他收益", "其他损失", "所得税费用", "以前年度损益调整"
    };

    /// <summary>判断某科目类别是否属于损益类。null/空返回 false。</summary>
    public static bool IsProfitLoss(string? category)
        => category != null && Array.IndexOf(ProfitLossCategories, category) >= 0;
}
