using System.Collections.Generic;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.Finance.Services;
using Xunit;

namespace STOTOP.Module.Finance.Tests.Amoeba;

/// <summary>
/// 批次5-S2 公共费分摊「接入」的纯函数辅助方法单测（设计 spec §4.1/§4.2/§4.4）。
/// 引擎本身的比例/除零/配对在 <see cref="CommonCostAllocationEngineTests"/>，此处只测接线侧取数口径。
/// </summary>
public class AmoebaCommonCostWiringTests
{
    private static FinAmoebaPLItem Item(long fid, string name, string role, string? alloc = null) =>
        new() { FID = fid, FItemName = name, FNodeRole = role, F分摊方式 = alloc };

    [Theory]
    [InlineData("volume", true)]
    [InlineData("VOLUME", true)]   // 大小写不敏感
    [InlineData("direct", false)]
    [InlineData(null, false)]
    public void IsCommonCostLeaf_true_only_for_volume(string? alloc, bool expected)
    {
        Assert.Equal(expected, AmoebaPLService.IsCommonCostLeaf(Item(1, "房租", "data", alloc)));
    }

    [Fact]
    public void ResolveVolumeIndicatorIds_matches_send_and_deliver_by_name()
    {
        var items = new List<FinAmoebaPLItem>
        {
            Item(10, "发件票量", "indicator"),
            Item(11, "派件票量", "indicator"),
            Item(12, "发件票量(数据)", "data"),   // 非 indicator 角色不计入
        };

        var (send, deliver) = AmoebaPLService.ResolveVolumeIndicatorIds(items);

        Assert.Equal(10, send);
        Assert.Equal(11, deliver);
    }

    [Fact]
    public void ResolveVolumeIndicatorIds_returns_null_when_absent()
    {
        var (send, deliver) = AmoebaPLService.ResolveVolumeIndicatorIds(
            new List<FinAmoebaPLItem> { Item(1, "出港收入", "data") });

        Assert.Null(send);
        Assert.Null(deliver);
    }

    [Fact]
    public void ComputeVolumeBasis_send_from_billing_waybill_count_only()
    {
        var points = new List<DataPoint>
        {
            new() { Source = "billing", WaybillCount = 300 },
            new() { Source = "billing", WaybillCount = 200 },
            new() { Source = "voucher", WaybillCount = 0, Amount = 9999m },   // 凭证不带件量
        };

        var vb = AmoebaPLService.ComputeVolumeBasis(points, new Dictionary<long, decimal>(), sendId: 10, deliverCount: 0L);

        Assert.Equal(500, vb.SendCount);
        Assert.Equal(0, vb.DeliverCount);
    }

    [Fact]
    public void ComputeVolumeBasis_send_falls_back_to_send_indicator_when_no_billing()
    {
        var points = new List<DataPoint> { new() { Source = "voucher", Amount = 100m } };
        var matched = new Dictionary<long, decimal> { [10] = 420m };   // 发件票量 indicator 匹配值

        var vb = AmoebaPLService.ComputeVolumeBasis(points, matched, sendId: 10, deliverCount: 0L);

        Assert.Equal(420, vb.SendCount);
    }

    [Fact]
    public void ComputeVolumeBasis_deliver_uses_injected_value_not_matched()
    {
        // 派件量接入 §B1：deliver 改由调用方注入（来自 STG申通派件日明细），
        // matched 里残留的旧派件票量 indicator 值必须被忽略。
        var points = new List<DataPoint> { new() { Source = "billing", WaybillCount = 500 } };
        var matched = new Dictionary<long, decimal> { [11] = 999m };   // 旧 deliver indicator，应被忽略

        var vb = AmoebaPLService.ComputeVolumeBasis(points, matched, sendId: 10, deliverCount: 180L);

        Assert.Equal(500, vb.SendCount);    // billing 优先于 send indicator
        Assert.Equal(180, vb.DeliverCount); // 取注入值 180 而非 matched[11]=999
    }

    [Fact]
    public void ComputeVolumeBasis_null_send_id_yields_zero_send_without_throwing()
    {
        var points = new List<DataPoint> { new() { Source = "voucher", Amount = 100m } };

        var vb = AmoebaPLService.ComputeVolumeBasis(points, new Dictionary<long, decimal>(), sendId: null, deliverCount: 0L);

        Assert.Equal(0, vb.SendCount);
        Assert.Equal(0, vb.DeliverCount);
    }

    // ===== ApplyCommonCostAllocation：deliver 双注入（scoped/full）的接线测试（派件量接入 §B2）=====
    // MatchDataPointsToPLItems 纯内存匹配（不碰 DB），故可精简实例化（其余依赖传 null!，仅用 _allocEngine）。

    private static AmoebaPLService NewWiringService() =>
        new(null!, null!, null!, null!, null!, null!, new CommonCostAllocationEngine());

    /// <summary>voucher 源公共费叶（按 deliver 基数分摊），配关联科目使 fullPoints 中 voucher 点命中产生全额。</summary>
    private static FinAmoebaPLItem CommonLeaf(long fid, string name, string basis) =>
        new()
        {
            FID = fid,
            FItemName = name,
            FNodeRole = "data",
            F分摊方式 = "volume",
            F分摊基数 = basis,
            F系统数据源 = "voucher",
            FRelatedAccountsJson = "[\"5401\"]",
        };

    private static DataPoint VoucherPoint(decimal amount) =>
        new() { Source = "voucher", Amount = amount, AccountCode = "540101" };

    [Fact]
    public void ApplyCommonCostAllocation_deliver_leaf_uses_injected_scoped_and_full_deliver()
    {
        var svc = NewWiringService();
        var deliverLeaf = CommonLeaf(601, "进港操作工资", "deliver");
        var plItems = new List<FinAmoebaPLItem> { deliverLeaf };

        // fullPoints 命中叶 → fullTotal=1000；scopedMatched 已有该叶直接归段残值(应被分摊额覆盖)。
        var fullPoints = new List<DataPoint> { VoucherPoint(1000m) };
        var scopedPoints = new List<DataPoint> { VoucherPoint(250m) };
        var scopedMatched = new Dictionary<long, decimal> { [601] = 250m };
        var warnings = new List<string>();

        // 注入 deliver：scoped=120、full=400 → deliver 基数分摊额 = 1000 × 120/400 = 300。
        svc.ApplyCommonCostAllocation(
            scopedMatched, scopedPoints, fullPoints, plItems,
            new List<FinAmoebaPLItem>(), plItems, warnings,
            scopedDeliver: 120L, fullDeliver: 400L);

        Assert.Equal(300m, scopedMatched[601]);   // 全额 × scopedDeliver/fullDeliver，证明 full 侧 deliver 真注入(非0)
    }

    [Fact]
    public void ApplyCommonCostAllocation_total_leaf_uses_send_plus_deliver_basis()
    {
        var svc = NewWiringService();
        var totalLeaf = CommonLeaf(602, "折旧", "total");
        var plItems = new List<FinAmoebaPLItem> { totalLeaf };

        // send 件量来自 billing WaybillCount：scoped 100、full 400。
        var fullPoints = new List<DataPoint>
        {
            VoucherPoint(1000m),
            new() { Source = "billing", WaybillCount = 400, IsPriced = true },
        };
        var scopedPoints = new List<DataPoint>
        {
            VoucherPoint(250m),
            new() { Source = "billing", WaybillCount = 100, IsPriced = true },
        };
        var scopedMatched = new Dictionary<long, decimal> { [602] = 250m };
        var warnings = new List<string>();

        // total 基数：scope total = send100+deliver100 = 200；full total = send400+deliver200 = 600。
        // 分摊额 = 1000 × 200/600。
        svc.ApplyCommonCostAllocation(
            scopedMatched, scopedPoints, fullPoints, plItems,
            new List<FinAmoebaPLItem>(), plItems, warnings,
            scopedDeliver: 100L, fullDeliver: 200L);

        Assert.Equal(Math.Round(1000m * 200 / 600, 2), scopedMatched[602]);
    }
}
