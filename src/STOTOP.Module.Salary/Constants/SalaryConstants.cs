namespace STOTOP.Module.Salary.Constants;

/// <summary>
/// 薪酬计算默认参数（后续可改为从系统参数表读取）
/// </summary>
public static class SalaryConstants
{
    /// <summary>社保个人比例（养老8% + 医疗2% + 失业0.5% = 10.5%）</summary>
    public const decimal SocialInsuranceRate = 0.105m;

    /// <summary>公积金个人比例（12%）</summary>
    public const decimal HousingFundRate = 0.12m;

    /// <summary>个税起征额（元/月）</summary>
    public const decimal TaxExemptionAmount = 5000m;

    /// <summary>B分兑换比例（1B分=0.1元，即10B分=1元）</summary>
    public const decimal BScoreToYuanRate = 0.1m;

    /// <summary>
    /// 7级超额累进税率表（月度）
    /// </summary>
    public static readonly (decimal UpperLimit, decimal Rate, decimal QuickDeduction)[] TaxBrackets = new[]
    {
        (3000m, 0.03m, 0m),
        (12000m, 0.10m, 210m),
        (25000m, 0.20m, 1410m),
        (35000m, 0.25m, 2660m),
        (55000m, 0.30m, 4410m),
        (80000m, 0.35m, 7160m),
        (decimal.MaxValue, 0.45m, 15160m)
    };
}
