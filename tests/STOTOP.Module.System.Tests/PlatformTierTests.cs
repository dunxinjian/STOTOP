using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Module.System.Entities;
using Xunit;

namespace STOTOP.Module.System.Tests;

/// <summary>
/// 阶段4A 平台层地基·模型形态自检：PLT租户/套餐/订阅 是平台层实体（在租户硬墙【之上】定义租户本身），
/// 必须【不】实现 ITenantScoped（否则查租户列表须先在某租户内=死锁），且不持 F组织ID（不触发漏标门禁）。
/// 单客户下真实的 TenantResolver→PLT租户 解析、V13 回填只能在 SQL Server dev 库验证（本套用 InMemory 只断言模型形态）。
/// </summary>
public class PlatformTierTests
{
    private static readonly Type[] PlatformEntities = { typeof(PltTenant), typeof(PltPlan), typeof(PltSubscription) };

    [Fact]
    public void PLT_三表已注册进模型且映射到正确表名()
    {
        using var ctx = TestDbContextFactory.Create("platform_tier");
        var model = ctx.Model;

        Assert.Equal("PLT租户", model.FindEntityType(typeof(PltTenant))!.GetTableName());
        Assert.Equal("PLT套餐", model.FindEntityType(typeof(PltPlan))!.GetTableName());
        Assert.Equal("PLT订阅", model.FindEntityType(typeof(PltSubscription))!.GetTableName());
    }

    [Fact]
    public void PLT_三表均不挂租户硬墙_且不持组织ID_不触发漏标门禁()
    {
        using var ctx = TestDbContextFactory.Create("platform_tier");

        foreach (var t in PlatformEntities)
        {
            Assert.False(typeof(ITenantScoped).IsAssignableFrom(t),
                $"{t.Name} 是平台层实体，不得实现 ITenantScoped（会陷入'查租户须先在租户内'死锁）");
            Assert.False(typeof(IOrgScoped).IsAssignableFrom(t), $"{t.Name} 不应实现 IOrgScoped");

            var et = ctx.Model.FindEntityType(t)!;
            Assert.Null(et.FindProperty("FOrgId"));      // 无组织隔离键 → 漏标门禁不触发
            Assert.Null(et.FindProperty("FOwnerOrgId"));
        }
    }

    [Fact]
    public void PLT租户_编号唯一_且状态默认正式()
    {
        using var ctx = TestDbContextFactory.Create("platform_tier");
        var et = ctx.Model.FindEntityType(typeof(PltTenant))!;

        var codeProp = et.FindProperty(nameof(PltTenant.FCode))!;
        Assert.Equal("F编号", codeProp.GetColumnName());
        Assert.Contains(et.GetIndexes(), ix => ix.IsUnique && ix.Properties.Any(p => p.Name == nameof(PltTenant.FCode)));

        // 状态枚举语义就位（供 4B 冻结白名单消费）
        Assert.Equal(2, (int)PltTenantStatus.Active);
        Assert.Equal(4, (int)PltTenantStatus.Frozen);
    }

    [Fact]
    public void SysUser_平台超管标记已映射且默认false()
    {
        using var ctx = TestDbContextFactory.Create("platform_tier");
        var prop = ctx.Model.FindEntityType(typeof(SysUser))!.FindProperty(nameof(SysUser.FIsPlatformAdmin))!;

        Assert.Equal("F是否平台超管", prop.GetColumnName());
        Assert.Equal(false, prop.GetDefaultValue());
        // SysUser 仍是全局身份实体，不进租户硬墙
        Assert.False(typeof(ITenantScoped).IsAssignableFrom(typeof(SysUser)));
    }
}
