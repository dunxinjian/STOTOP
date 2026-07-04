using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.System.Dtos;
using STOTOP.Module.System.Entities;
using STOTOP.Module.System.Middleware;
using STOTOP.Module.System.Services;
using Xunit;
// STOTOP.Module.Task 命名空间遮蔽 System.Threading.Tasks.Task（本测试命名空间在 STOTOP.Module 下），故用 STT 别名。
using STT = System.Threading.Tasks;

namespace STOTOP.Module.System.Tests;

/// <summary>
/// 阶段4B 平台层运行时自检：PlatformService(PLT租户/套餐/订阅 CRUD) + 平台超管判定 + 欠费冻结中间件(D7)。
/// PLT 三表 + SYS用户 均非 ITenantScoped（无租户过滤器）→ 全部可 InMemory 验证。
/// 平台超管授权过滤器(PlatformOnlyAttribute)与作用域进入的端到端行为留 dev-DB/集成验证。
/// </summary>
public class PlatformRuntimeTests
{
    // ---- PlatformService ----

    [Fact]
    public async STT.Task 创建租户_编号重复被拒_状态默认试用()
    {
        using var ctx = TestDbContextFactory.Create("plat_rt");
        var svc = new PlatformService(ctx);

        var id = await svc.CreateTenantAsync(new CreatePlatformTenantRequest { Name = "甲租户", Code = "T-A", RootOrgId = 10 });
        var t = await svc.GetTenantAsync(id);
        Assert.NotNull(t);
        Assert.Equal((int)PltTenantStatus.Trial, t!.Status);
        Assert.Equal("试用", t.StatusName);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CreateTenantAsync(new CreatePlatformTenantRequest { Name = "重复", Code = "T-A", RootOrgId = 11 }));
    }

    [Fact]
    public async STT.Task 更新租户状态_冻结与非法值()
    {
        using var ctx = TestDbContextFactory.Create("plat_rt");
        var svc = new PlatformService(ctx);
        var id = await svc.CreateTenantAsync(new CreatePlatformTenantRequest { Name = "乙", Code = "T-B", RootOrgId = 12 });

        await svc.UpdateTenantStatusAsync(id, (int)PltTenantStatus.Frozen);
        Assert.Equal((int)PltTenantStatus.Frozen, (await svc.GetTenantAsync(id))!.Status);

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.UpdateTenantStatusAsync(id, 99));
    }

    [Fact]
    public async STT.Task 订阅生效_把租户置正式并写套餐与到期()
    {
        using var ctx = TestDbContextFactory.Create("plat_rt");
        var svc = new PlatformService(ctx);
        var tenantId = await svc.CreateTenantAsync(new CreatePlatformTenantRequest { Name = "丙", Code = "T-C", RootOrgId = 13 });
        var planId = await svc.CreatePlanAsync(new SavePlatformPlanRequest { Name = "标准版", Code = "PLAN-STD", MaxUsers = 50 });

        var start = new DateTime(2026, 1, 1);
        var end = new DateTime(2026, 12, 31);
        await svc.CreateSubscriptionAsync(new CreateSubscriptionRequest { TenantId = tenantId, PlanId = planId, PeriodStart = start, PeriodEnd = end });

        var t = await svc.GetTenantAsync(tenantId);
        Assert.Equal((int)PltTenantStatus.Active, t!.Status);   // 试用→正式
        Assert.Equal(planId, t.PlanId);
        Assert.Equal(end, t.ExpireAt);

        var subs = await svc.GetSubscriptionsAsync(tenantId);
        Assert.Single(subs);
        Assert.Equal(planId, subs[0].PlanId);
    }

    [Fact]
    public async STT.Task 订阅周期止不晚于起被拒_且套餐不存在被拒()
    {
        using var ctx = TestDbContextFactory.Create("plat_rt");
        var svc = new PlatformService(ctx);
        var tenantId = await svc.CreateTenantAsync(new CreatePlatformTenantRequest { Name = "丁", Code = "T-D", RootOrgId = 14 });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CreateSubscriptionAsync(new CreateSubscriptionRequest { TenantId = tenantId, PlanId = 999, PeriodStart = new(2026, 1, 1), PeriodEnd = new(2026, 6, 1) }));

        var planId = await svc.CreatePlanAsync(new SavePlatformPlanRequest { Name = "P", Code = "PLAN-X" });
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CreateSubscriptionAsync(new CreateSubscriptionRequest { TenantId = tenantId, PlanId = planId, PeriodStart = new(2026, 6, 1), PeriodEnd = new(2026, 1, 1) }));
    }

    // ---- 平台超管判定 ----

    [Fact]
    public async STT.Task 平台超管判定_仅认FIsPlatformAdmin位()
    {
        using var ctx = TestDbContextFactory.Create("plat_rt");
        ctx.Set<SysUser>().Add(new SysUser { FID = 100, FAccount = "boss", FName = "老板", FUID = "u100", FIsPlatformAdmin = true });
        ctx.Set<SysUser>().Add(new SysUser { FID = 101, FAccount = "clerk", FName = "员工", FUID = "u101", FIsPlatformAdmin = false });
        await ctx.SaveChangesAsync();

        var admin = new AdminAuthorizationService();
        Assert.True(await admin.IsPlatformAdminByUserIdAsync(ctx, 100));
        Assert.False(await admin.IsPlatformAdminByUserIdAsync(ctx, 101));
        Assert.False(await admin.IsPlatformAdminByUserIdAsync(ctx, 999)); // 不存在
    }

    // ---- 租户默认待办渠道解析(4E·D3) ----

    [Fact]
    public async STT.Task 租户待办渠道解析_按FDefaultTodoChannel映射_无租户回退空()
    {
        using var ctx = TestDbContextFactory.Create("chan");
        ctx.Set<PltTenant>().Add(new PltTenant { FID = 8001, FName = "甲", FCode = "CH-A", FRootOrgId = 8001, FDefaultTodoChannel = 1 });
        ctx.Set<PltTenant>().Add(new PltTenant { FID = 8002, FName = "乙", FCode = "CH-B", FRootOrgId = 8002, FDefaultTodoChannel = 2 });
        ctx.Set<PltTenant>().Add(new PltTenant { FID = 8003, FName = "丙", FCode = "CH-C", FRootOrgId = 8003, FDefaultTodoChannel = 3 });
        await ctx.SaveChangesAsync();
        var r = new TenantTodoChannelResolver(ctx);

        Assert.Equal(new[] { "dingtalk" }, await r.ResolveChannelNamesAsync(8001));
        Assert.Equal(new[] { "wecom" }, await r.ResolveChannelNamesAsync(8002));
        Assert.Equal(new[] { "dingtalk", "wecom" }, await r.ResolveChannelNamesAsync(8003)); // 双推
        Assert.Empty(await r.ResolveChannelNamesAsync(9999)); // 无该租户 → 空，调用方回退按待办自带渠道
    }

    // ---- 欠费冻结中间件(D7) ----

    [Fact]
    public async STT.Task 冻结租户_业务写与批量导出被拒_普通只读放行()
    {
        // 各用例用唯一 tenantId 避开进程级 15s 状态缓存串扰。
        Assert.Equal(402, await RunFreeze(7001, PltTenantStatus.Frozen, "/api/crm/customer", "POST"));   // 业务写
        Assert.Equal(402, await RunFreeze(7002, PltTenantStatus.Frozen, "/api/finance/report/export", "GET")); // 批量导出
        Assert.Equal(200, await RunFreeze(7003, PltTenantStatus.Frozen, "/api/finance/voucher/list", "GET")); // 结账类只读放行
        // 终审修：org-context 成员写不再整前缀豁免 → 冻结时被拒；切换/我的组织等导航只读仍放行。
        Assert.Equal(402, await RunFreeze(7008, PltTenantStatus.Frozen, "/api/system/org-context/user-organizations", "POST"));
        Assert.Equal(200, await RunFreeze(7009, PltTenantStatus.Frozen, "/api/system/org-context/switch", "POST"));
    }

    [Fact]
    public async STT.Task 正式租户_一切放行_跳过路径与无租户放行()
    {
        Assert.Equal(200, await RunFreeze(7004, PltTenantStatus.Active, "/api/crm/customer", "POST"));   // 正式:写放行
        Assert.Equal(200, await RunFreeze(7005, PltTenantStatus.Frozen, "/api/auth/login", "POST"));      // 冻结但登录跳过
        Assert.Equal(200, await RunFreeze(7006, PltTenantStatus.Frozen, "/api/platform/tenants", "POST")); // 冻结但平台(续费/解冻)跳过
        Assert.Equal(200, await RunFreeze(7007, null, "/api/crm/customer", "POST"));                       // 无租户上下文放行
    }

    /// <summary>构造带 InMemory DbContext 的请求跑冻结中间件；返回响应状态码（放行=200 表示 next 被调用）。</summary>
    private static async STT.Task<int> RunFreeze(long tenantId, PltTenantStatus? seedStatus, string path, string method)
    {
        var ctx = TestDbContextFactory.Create($"freeze_{tenantId}");
        if (seedStatus.HasValue)
        {
            ctx.Set<PltTenant>().Add(new PltTenant { FID = tenantId, FName = "X", FCode = $"C{tenantId}", FRootOrgId = tenantId, FStatus = (int)seedStatus.Value });
            await ctx.SaveChangesAsync();
        }

        var sp = new ServiceCollection().AddSingleton(ctx).BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = sp };
        http.Request.Path = path;
        http.Request.Method = method;
        http.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "1") }, "test"));
        if (tenantId != 7007) http.Items["CurrentTenantId"] = tenantId; // 7007 = 无租户上下文用例
        http.Response.Body = new MemoryStream();

        var nextCalled = false;
        var mw = new TenantFreezeMiddleware(_ => { nextCalled = true; return STT.Task.CompletedTask; });
        await mw.InvokeAsync(http);

        return nextCalled ? 200 : http.Response.StatusCode;
    }
}
