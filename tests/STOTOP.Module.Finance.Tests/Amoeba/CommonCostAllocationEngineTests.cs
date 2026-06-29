using STOTOP.Module.Finance.Entities;
using STOTOP.Module.Finance.Services;
using Xunit;

namespace STOTOP.Module.Finance.Tests.Amoeba;

/// <summary>批次5 公共费分摊引擎单测（纯函数，无 DB）。设计 spec §4。</summary>
public class CommonCostAllocationEngineTests
{
    private static readonly ICommonCostAllocationEngine Engine = new CommonCostAllocationEngine();

    private static FinAmoebaPLItem Leaf(long fid, string name, string? basis) =>
        new() { FID = fid, FItemName = name, F分摊方式 = "volume", F分摊基数 = basis };

    [Fact]
    public void Allocate_proportional_by_send_volume()
    {
        var leaves = new[] { Leaf(601, "房租", "send") };
        var full = new Dictionary<long, decimal> { [601] = 1000m };
        var scopeVol = new VolumeBasis { SendCount = 300 };
        var allVol = new VolumeBasis { SendCount = 1000 };

        var result = Engine.Allocate(leaves, full, scopeVol, allVol, out var warnings);

        Assert.Equal(300m, result[601]);          // 1000 × 300/1000
        Assert.Empty(warnings);
    }

    [Fact]
    public void Allocate_honors_basis_total_and_deliver()
    {
        var leaves = new[] { Leaf(601, "折旧", "total"), Leaf(602, "进港操作工资", "deliver") };
        var full = new Dictionary<long, decimal> { [601] = 1000m, [602] = 600m };
        // scope: send 100 + deliver 100 = total 200; all: send 400 + deliver 200 = total 600
        var scopeVol = new VolumeBasis { SendCount = 100, DeliverCount = 100 };
        var allVol = new VolumeBasis { SendCount = 400, DeliverCount = 200 };

        var result = Engine.Allocate(leaves, full, scopeVol, allVol, out _);

        Assert.Equal(Math.Round(1000m * 200 / 600, 2), result[601]); // total basis: 200/600
        Assert.Equal(300m, result[602]);                            // deliver basis: 600 × 100/200
    }

    [Fact]
    public void Allocate_zero_full_volume_returns_zero_with_warning()
    {
        var leaves = new[] { Leaf(601, "水电", "send") };
        var full = new Dictionary<long, decimal> { [601] = 500m };
        var scopeVol = new VolumeBasis { SendCount = 0 };
        var allVol = new VolumeBasis { SendCount = 0 };

        var result = Engine.Allocate(leaves, full, scopeVol, allVol, out var warnings);

        Assert.Equal(0m, result[601]);
        Assert.Single(warnings);
    }

    [Fact]
    public void Allocate_skips_missing_or_zero_total()
    {
        var leaves = new[] { Leaf(601, "缺全额", "send"), Leaf(602, "零全额", "send") };
        var full = new Dictionary<long, decimal> { [602] = 0m };   // 601 缺失, 602 为0
        var scopeVol = new VolumeBasis { SendCount = 100 };
        var allVol = new VolumeBasis { SendCount = 200 };

        var result = Engine.Allocate(leaves, full, scopeVol, allVol, out _);

        Assert.False(result.ContainsKey(601));   // 全额缺失 → 不注入
        Assert.False(result.ContainsKey(602));   // 全额为0 → 不注入
    }

    [Theory]
    [InlineData("send", 100)]
    [InlineData(null, 100)]
    [InlineData("deliver", 40)]
    [InlineData("total", 140)]
    public void VolumeBasis_ByKind(string? kind, long expected)
    {
        var vb = new VolumeBasis { SendCount = 100, DeliverCount = 40 };
        Assert.Equal(expected, vb.ByKind(kind));
    }

    [Fact]
    public void Reconcile_flags_balance_within_tolerance()
    {
        var balanced = Engine.Reconcile(601, 1000m, new[] { 300m, 250m, 449.97m });   // diff 0.03 ≤ 0.05
        Assert.True(balanced.IsBalanced);
        Assert.Equal(0.03m, Math.Round(balanced.Diff, 2));

        var off = Engine.Reconcile(601, 1000m, new[] { 300m, 250m, 449m });           // diff 1.00 > 0.05
        Assert.False(off.IsBalanced);
        Assert.Equal(1m, off.Diff);
    }
}
