using Microsoft.Extensions.Logging.Abstractions;
using STOTOP.Module.CardFlow.Models;
using STOTOP.Module.CardFlow.Services.Handlers;
using Xunit;

namespace STOTOP.Module.CardFlow.Tests.Handlers;

/// <summary>
/// [缺陷3] AutoVoucherAuxiliaryResolver.ResolveFixed 编码兜底回归。
/// express_brand 等固定项 FID 由 EXP品牌 INSERT...SELECT 自增同步、非稳定标识；re-seed/组织树重建后
/// FID 漂移时，须靠稳定编码(ST/YD/JT)兜回，不再静默丢弃。FID 命中时保留精确匹配、行为不变。
/// 纯内存逻辑，走 public ResolveAuxiliary(fixed 模式不读 row)。
/// </summary>
public class AutoVoucherAuxiliaryResolverTests
{
    private static AutoVoucherAuxiliaryResolver Build(params AuxiliaryItemInfo[] items)
    {
        var r = new AutoVoucherAuxiliaryResolver(NullLogger.Instance);
        r.Initialize(items);
        return r;
    }

    private static AuxiliaryItemInfo Brand(long id, string code, string name)
        => new() { Id = id, AuxType = "express_brand", Code = code, Name = name };

    private static long? ResolveId(AutoVoucherAuxiliaryResolver r, AuxiliaryConfigV2 cfg)
    {
        var res = r.ResolveAuxiliary(new Dictionary<string, object>(), new List<AuxiliaryConfigV2> { cfg });
        return res.Count > 0 ? res[0].Id : (long?)null;
    }

    [Fact]
    public void FID命中_走FID精确匹配_行为不变()
    {
        var r = Build(Brand(16, "ST", "申通"), Brand(17, "YD", "韵达"));
        var id = ResolveId(r, new AuxiliaryConfigV2
        {
            AuxType = "express_brand", SourceType = "fixed", FixedItemId = 16, FixedItemCode = "ST"
        });
        Assert.Equal(16L, id);
    }

    [Fact]
    public void FID漂移_落编码兜底_不丢不误配()
    {
        // 模拟 re-seed：ST 项 FID 已变成 99（配置里仍写死 fixedItemId:16，16 不在候选中）
        var r = Build(Brand(99, "ST", "申通"), Brand(100, "YD", "韵达"));
        var id = ResolveId(r, new AuxiliaryConfigV2
        {
            AuxType = "express_brand", SourceType = "fixed", FixedItemId = 16, FixedItemCode = "ST"
        });
        Assert.Equal(99L, id); // 靠 FixedItemCode="ST" 兜回，而非返回 null（旧实现会静默丢）
    }

    [Fact]
    public void 无FID仅编码_按编码命中()
    {
        var r = Build(Brand(5, "YD", "韵达"));
        var id = ResolveId(r, new AuxiliaryConfigV2
        {
            AuxType = "express_brand", SourceType = "fixed", FixedItemId = null, FixedItemCode = "YD"
        });
        Assert.Equal(5L, id);
    }

    [Fact]
    public void FID与编码均不命中_落FixedValue()
    {
        var r = Build(Brand(7, "JT", "极兔"));
        var id = ResolveId(r, new AuxiliaryConfigV2
        {
            AuxType = "express_brand", SourceType = "fixed", FixedItemId = 16, FixedItemCode = null, FixedValue = "JT"
        });
        Assert.Equal(7L, id);
    }

    [Fact]
    public void 全不命中_返回空_由上层记warning()
    {
        var r = Build(Brand(1, "ST", "申通"));
        var id = ResolveId(r, new AuxiliaryConfigV2
        {
            AuxType = "express_brand", SourceType = "fixed", FixedItemId = 16, FixedItemCode = "YD", FixedValue = "YD"
        });
        Assert.Null(id);
    }
}
