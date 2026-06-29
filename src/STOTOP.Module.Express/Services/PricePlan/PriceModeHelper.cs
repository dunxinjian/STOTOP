namespace STOTOP.Module.Express.Services.PricePlan;

/// <summary>
/// cell 级 mode 派生帮助类。
/// A3' 改造后 cell 级 mode 由字段存在性派生，本类是唯一权威入口。
/// 段级 FPricingMethod 由 Service 在保存时按 cell 重算并落库。
/// </summary>
public static class PriceModeHelper
{
    /// <summary>
    /// 根据 cell 续重价格派生 cell 级 mode。
    /// 规则：FContinuePrice 非空 ⇔ 3（首续重）；NULL ⇔ 1（固定单价）。
    /// </summary>
    public static int DeriveCellMode(decimal? continuePrice)
        => continuePrice.HasValue ? 3 : 1;
}
