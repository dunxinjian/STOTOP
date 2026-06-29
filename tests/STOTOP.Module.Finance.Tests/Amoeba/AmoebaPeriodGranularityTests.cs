using System;
using STOTOP.Module.Finance.Services;
using Xunit;

namespace STOTOP.Module.Finance.Tests.Amoeba;

/// <summary>
/// 批次5-S3 周期粒度纯函数单测（设计 spec §5.3）。
/// PeriodToDateRange 返回各粒度「闭区间末日」（聚合内部 &lt; end.AddDays(1) 自动转半开）；
/// CalcYoyPeriod 对 day/week 返回 null（无同比意义）。
/// </summary>
public class AmoebaPeriodGranularityTests
{
    [Theory]
    [InlineData("202603", "month", "2026-03-01", "2026-03-31")]
    [InlineData("20260315", "day", "2026-03-15", "2026-03-15")]
    [InlineData("2026-W11", "week", "2026-03-09", "2026-03-15")]   // ISO 2026 第11周：周一 3/9 → 周日 3/15
    [InlineData("2026Q2", "quarter", "2026-04-01", "2026-06-30")]
    [InlineData("2026", "year", "2026-01-01", "2026-12-31")]
    public void PeriodToDateRange_returns_inclusive_last_day_per_granularity(
        string period, string granularity, string start, string end)
    {
        var (s, e) = AmoebaPLService.PeriodToDateRange(period, granularity);
        Assert.Equal(DateTime.Parse(start), s);
        Assert.Equal(DateTime.Parse(end), e);
    }

    [Fact]
    public void PeriodToDateRange_defaults_to_month()
    {
        var (s, e) = AmoebaPLService.PeriodToDateRange("202602");
        Assert.Equal(new DateTime(2026, 2, 1), s);
        Assert.Equal(new DateTime(2026, 2, 28), e);
    }

    [Theory]
    [InlineData("202603", "month", true)]
    [InlineData("202613", "month", false)]   // 月份越界
    [InlineData("2026", "month", false)]      // 位数不符
    [InlineData("20260315", "day", true)]
    [InlineData("20260230", "day", false)]    // 2月30日非法
    [InlineData("2026-W11", "week", true)]
    [InlineData("2026-W54", "week", false)]   // 周越界
    [InlineData("2026Q1", "quarter", true)]
    [InlineData("2026Q5", "quarter", false)]  // 季越界
    [InlineData("2026", "year", true)]
    [InlineData("abcd", "year", false)]
    public void IsValidPeriod_validates_format_per_granularity(string period, string granularity, bool expected)
    {
        Assert.Equal(expected, AmoebaPLService.IsValidPeriod(period, granularity));
    }

    [Theory]
    [InlineData("202601", "month", "202512")]
    [InlineData("202603", "month", "202602")]
    [InlineData("20260301", "day", "20260228")]
    [InlineData("2026-W02", "week", "2026-W01")]
    [InlineData("2026Q1", "quarter", "2025Q4")]
    [InlineData("2026Q3", "quarter", "2026Q2")]
    [InlineData("2026", "year", "2025")]
    public void CalcPreviousPeriod_steps_back_one_unit(string period, string granularity, string expected)
    {
        Assert.Equal(expected, AmoebaPLService.CalcPreviousPeriod(period, granularity));
    }

    [Theory]
    [InlineData("202603", "month", "202503")]
    [InlineData("2026Q2", "quarter", "2025Q2")]
    [InlineData("2026", "year", "2025")]
    public void CalcYoyPeriod_subtracts_one_year_for_ledger_granularities(string period, string granularity, string expected)
    {
        Assert.Equal(expected, AmoebaPLService.CalcYoyPeriod(period, granularity));
    }

    [Theory]
    [InlineData("20260315", "day")]
    [InlineData("2026-W11", "week")]
    public void CalcYoyPeriod_returns_null_for_sub_month_granularities(string period, string granularity)
    {
        Assert.Null(AmoebaPLService.CalcYoyPeriod(period, granularity));
    }

    [Theory]
    [InlineData("202603", "month", "M:202603")]
    [InlineData("20260315", "day", "D:20260315")]
    [InlineData("2026-W11", "week", "W:2026-W11")]
    [InlineData("2026Q1", "quarter", "Q:2026Q1")]
    [InlineData("2026", "year", "Y:2026")]
    public void BuildPeriodKey_prefixes_by_granularity(string period, string granularity, string expected)
    {
        Assert.Equal(expected, AmoebaPLService.BuildPeriodKey(period, granularity));
    }

    [Theory]
    [InlineData(null, "month")]
    [InlineData("", "month")]
    [InlineData("  ", "month")]
    [InlineData("MONTH", "month")]
    [InlineData("Day", "day")]
    public void NormalizeGranularity_defaults_and_lowercases(string? input, string expected)
    {
        Assert.Equal(expected, AmoebaPLService.NormalizeGranularity(input));
    }

    [Fact]
    public void NormalizeGranularity_throws_on_unknown()
    {
        Assert.Throws<ArgumentException>(() => AmoebaPLService.NormalizeGranularity("fortnight"));
    }

    [Theory]
    [InlineData("month", true)]
    [InlineData("quarter", true)]
    [InlineData("year", true)]
    [InlineData("day", false)]
    [InlineData("week", false)]
    public void IsLedgerGranularity_true_for_month_quarter_year(string g, bool expected)
    {
        Assert.Equal(expected, AmoebaPLService.IsLedgerGranularity(g));
    }

    // C1 互斥选源（设计 §5.1 + 当月例外）：billing 两类粒度共享；台账粒度(月/季/年)恒用 voucher+depreciation，
    // 仅当期间**未结**(当月/未来)才叠加 estimate(账期未结、凭证不全的临时填充)，已结过往期间不取 estimate；
    // 估算粒度(日/周)恒用 estimate 不取 voucher/depreciation。
    [Theory]
    // 台账粒度：未结(periodClosed=false)→含估值；已结(true)→仅台账
    [InlineData("month", false, true, true, true, true)]
    [InlineData("month", true, true, true, true, false)]
    [InlineData("quarter", false, true, true, true, true)]
    [InlineData("quarter", true, true, true, true, false)]
    [InlineData("year", false, true, true, true, true)]
    [InlineData("year", true, true, true, true, false)]
    [InlineData(null, false, true, true, true, true)]    // 默认 month，未结
    [InlineData(null, true, true, true, true, false)]    // 默认 month，已结
    // 估算粒度：estimate 恒取，periodClosed 不影响
    [InlineData("day", false, true, false, false, true)]
    [InlineData("day", true, true, false, false, true)]
    [InlineData("week", false, true, false, false, true)]
    [InlineData("week", true, true, false, false, true)]
    public void SelectSources_applies_C1_with_current_period_estimate(
        string? granularity, bool periodClosed, bool billing, bool voucher, bool depreciation, bool estimate)
    {
        var src = AmoebaPLService.SelectSources(granularity, periodClosed);
        Assert.Equal(billing, src.Billing);
        Assert.Equal(voucher, src.Voucher);
        Assert.Equal(depreciation, src.Depreciation);
        Assert.Equal(estimate, src.Estimate);
    }

    [Theory]
    [InlineData("20260315", "day", "202603")]
    [InlineData("20260101", "day", "202601")]
    [InlineData("2026-W11", "week", "202603")]    // 周一 2026-03-09 → 202603
    [InlineData("2026-W01", "week", "202512")]    // 跨年周归周一所在月：周一 2025-12-29 → 202512
    [InlineData("202603", "month", "202603")]
    [InlineData("2026Q2", "quarter", "202604")]   // 季初 4 月
    public void PeriodContainingMonth_returns_start_month(string period, string granularity, string expected)
    {
        Assert.Equal(expected, AmoebaPLService.PeriodContainingMonth(period, granularity));
    }

    [Theory]
    [InlineData("202602", "month", "2026-03-15", true)]    // 上月已结
    [InlineData("202603", "month", "2026-03-15", false)]   // 当月未结
    [InlineData("202604", "month", "2026-03-15", false)]   // 未来未结
    [InlineData("202603", "month", "2026-03-31", false)]   // 当月最后一天仍未结
    [InlineData("202603", "month", "2026-04-01", true)]    // 次月起当月已结
    [InlineData("20260314", "day", "2026-03-15", true)]    // 昨日已结
    [InlineData("20260315", "day", "2026-03-15", false)]   // 当日未结
    [InlineData("2026Q1", "quarter", "2026-03-15", false)] // 当季未结
    [InlineData("2026Q1", "quarter", "2026-04-01", true)]  // 次季起已结
    public void IsPeriodClosed_true_when_period_fully_elapsed(string period, string granularity, string asOf, bool expected)
    {
        Assert.Equal(expected, AmoebaPLService.IsPeriodClosed(period, granularity, DateTime.Parse(asOf)));
    }
}
