using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Services;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.System.Services;
using Xunit;
// System.Tests 引用 STOTOP.Module.Task → 裸 Task 被遮蔽，用 STT 别名。
using STT = System.Threading.Tasks;

namespace STOTOP.Module.System.Tests;

/// <summary>
/// 钉钉 per-tenant 配置地基自检：根租户走 JSON 全局(测试无文件=null,且不误读 DB)、非根租户读写走 SYS钉钉配置 表。
/// </summary>
public class DingTalkTenantConfigTests
{
    private sealed class FakeResolver : ITenantResolver
    {
        public long? GetRootTenantId() => 1;
        public long? ResolveTenantForOrg(long orgId) => 1;
    }

    // 平台作用域桩：Enter 置 IsPlatformScope=true（令跨租户写 SysDingTalkConfig 不被 fail-closed 挡），Dispose 复位。
    private sealed class FakePlatformScope : IPlatformScopeFactory
    {
        private readonly TestDbContextFactory.TestContextAccessor _a;
        public FakePlatformScope(TestDbContextFactory.TestContextAccessor a) => _a = a;
        public IDisposable Enter(string reason)
        {
            var prev = _a.IsPlatformScope;
            _a.IsPlatformScope = true;
            return new Reset(() => _a.IsPlatformScope = prev);
        }
        private sealed class Reset : IDisposable
        {
            private readonly Action _d;
            public Reset(Action d) => _d = d;
            public void Dispose() => _d();
        }
    }

    private static (STOTOPDbContext db, TestDbContextFactory.TestContextAccessor acc) NewDb()
    {
        TenantTestModules.RegisterAll();
        var acc = new TestDbContextFactory.TestContextAccessor { CurrentOrgId = null, CurrentTenantId = 1, IsPlatformScope = false };
        var opts = new DbContextOptionsBuilder<STOTOPDbContext>()
            .UseInMemoryDatabase($"dt_cfg_{Guid.NewGuid():N}")
            .Options;
        return (new STOTOPDbContext(opts, acc), acc);
    }

    [Fact]
    public async STT.Task 非根租户配置_读写走DB表_未配置返null()
    {
        var (db, acc) = NewDb();
        var svc = new DingTalkTenantConfigService(db, new FakeResolver(), acc, new FakePlatformScope(acc));

        await svc.UpsertForTenantAsync(20, new DingTalkConfigRecord
        {
            CorpId = "corp20",
            AppKey = "k20",
            AppSecret = "s20",
            IsEnabled = 1,
        });

        var got = await svc.GetForTenantAsync(20);
        Assert.NotNull(got);
        Assert.Equal("corp20", got!.CorpId);
        Assert.Equal("k20", got.AppKey);
        Assert.Equal(1, got.IsEnabled);

        // 未配置的非根租户 → null
        Assert.Null(await svc.GetForTenantAsync(999));
    }

    [Fact]
    public async STT.Task 根租户_走JSON全局_不误读DB表()
    {
        var (db, acc) = NewDb();
        var svc = new DingTalkTenantConfigService(db, new FakeResolver(), acc, new FakePlatformScope(acc));

        // 先给非根租户 20 建行
        await svc.UpsertForTenantAsync(20, new DingTalkConfigRecord { CorpId = "corp20", AppKey = "k20", IsEnabled = 1 });

        // 根租户(1) → 走 JSON 全局；测试环境无 dingtalk-config.json → null；且绝不返回租户20的 DB 行。
        var root = await svc.GetForTenantAsync(1);
        Assert.Null(root);
    }
}
