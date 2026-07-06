using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Infrastructure.Data;
using STOTOP.Infrastructure.Events;
using STOTOP.Module.System.Dtos;
using STOTOP.Module.System.Entities;
using STOTOP.Module.System.Services;
using STOTOP.Module.System.Services.Interfaces;
using Xunit;
using STT = System.Threading.Tasks;

namespace STOTOP.Module.System.Tests;

/// <summary>
/// R5·stage4C 越权阻断自检：区分平台级/租户级 admin，且租户级 admin 的用户/角色/组织/岗位管理被收敛到本租户。
/// 覆盖任务要求：租户 admin 拿不到他租户用户、不能重置他租户密码、不能授全局 admin 角色提权；平台 admin 不受限（回归）。
/// </summary>
public class TenantAdminIsolationTests
{
    private const long TenantA = 1001;
    private const long TenantB = 2002;
    private const long PlatformAdminRoleId = 1;   // 全局 admin 角色（FScope=platform, FIsAdmin）
    private const long TenantAAdminRoleId = 10;   // 租户A私有 admin 角色（FScope=tenant, FIsAdmin）

    private const long PlatformAdminUserId = 1;   // account=admin, FIsPlatformAdmin
    private const long TenantAAdminUserId = 100;
    private const long TenantAUserId = 101;
    private const long TenantBUserId = 200;

    private static readonly TenantDataScope ScopeA = new(false, new[] { TenantA });
    private static readonly TenantDataScope ScopePlatform = new(true, Array.Empty<long>());

    private static STOTOPDbContext Seed(string db)
    {
        var ctx = TestDbContextFactory.Create(db, platformScope: true, tenantId: null);

        // 组织类型（GetTreeAsync 的 Include(OrgType) 是必需外键=INNER JOIN，无匹配类型会丢行）
        ctx.Set<SysOrgType>().Add(new SysOrgType { FID = 5, FCode = "DEPT", FName = "部门", FKind = (int)OrgKind.Dept });

#pragma warning disable CS0618
        ctx.Set<SysOrganization>().AddRange(
            new SysOrganization { FID = TenantA, FName = "租户A", FCode = "TA", FParentId = 0, FTypeId = 5, FTenantId = TenantA },
            new SysOrganization { FID = TenantB, FName = "租户B", FCode = "TB", FParentId = 0, FTypeId = 5, FTenantId = TenantB });
#pragma warning restore CS0618

        ctx.Set<SysRole>().AddRange(
            new SysRole { FID = PlatformAdminRoleId, FName = "平台管理员", FCode = "ADMIN", FScope = SysRoleScope.Platform, FIsAdmin = true },
            new SysRole { FID = TenantAAdminRoleId, FName = "租户A管理员", FCode = "TENANT_ADMIN_TA", FScope = SysRoleScope.Tenant, FTenantId = TenantA, FIsAdmin = true });

        ctx.Set<SysUser>().AddRange(
            new SysUser { FID = PlatformAdminUserId, FName = "平台超管", FAccount = "admin", FPasswordHash = "x", FIsPlatformAdmin = true },
            new SysUser { FID = TenantAAdminUserId, FName = "A管理员", FAccount = "a_admin", FPasswordHash = "x" },
            new SysUser { FID = TenantAUserId, FName = "A用户1", FAccount = "a_user1", FPasswordHash = "x" },
            new SysUser { FID = TenantBUserId, FName = "B用户1", FAccount = "b_user1", FPasswordHash = "hashB" });

        ctx.Set<SysUserRole>().AddRange(
            new SysUserRole { FUserId = PlatformAdminUserId, FRoleId = PlatformAdminRoleId },
            new SysUserRole { FUserId = TenantAAdminUserId, FRoleId = TenantAAdminRoleId });

        ctx.Set<SysTenantMember>().AddRange(
            new SysTenantMember { FUserId = PlatformAdminUserId, FTenantId = TenantA, FInviteStatus = 2 },
            new SysTenantMember { FUserId = TenantAAdminUserId, FTenantId = TenantA, FInviteStatus = 2 },
            new SysTenantMember { FUserId = TenantAUserId, FTenantId = TenantA, FInviteStatus = 2 },
            new SysTenantMember { FUserId = TenantBUserId, FTenantId = TenantB, FInviteStatus = 2 });

        ctx.SaveChanges();
        return ctx;
    }

    private static UserService UserSvc(STOTOPDbContext ctx, TenantDataScope scope)
        => new(ctx, new NullHttp(), new NoopChangeLog(), new NoopEvents(), new StubScope(scope));

    private static RoleService RoleSvc(STOTOPDbContext ctx, TenantDataScope scope)
        => new(ctx, new NoopCodeRule(), new StubScope(scope));

    private static OrganizationService OrgSvc(STOTOPDbContext ctx, TenantDataScope scope)
        => new(ctx, new NullHttp(), new NoopChangeLog(), new NoopEvents(), new StubScope(scope));

    private static PositionService PosSvc(STOTOPDbContext ctx, TenantDataScope scope)
        => new(ctx, new NullHttp(), new NoopChangeLog(), new StubScope(scope));

    // ========== A1：作用域解析 ==========

    [Fact]
    public async STT.Task 平台admin_解析为platform_不限租户()
    {
        using var ctx = Seed(nameof(平台admin_解析为platform_不限租户));
        var scope = await new AdminAuthorizationService().ResolveAdminScopeAsync(ctx, PlatformAdminUserId);
        Assert.True(scope.IsAdmin);
        Assert.True(scope.IsPlatformAdmin);
        Assert.Empty(scope.TenantIds);
    }

    [Fact]
    public async STT.Task 租户admin_解析为tenant_带本租户id()
    {
        using var ctx = Seed(nameof(租户admin_解析为tenant_带本租户id));
        var scope = await new AdminAuthorizationService().ResolveAdminScopeAsync(ctx, TenantAAdminUserId);
        Assert.True(scope.IsAdmin);
        Assert.False(scope.IsPlatformAdmin);
        Assert.Equal(new[] { TenantA }, scope.TenantIds);
    }

    // ========== A2：JWT claim 构造 ==========

    [Fact]
    public void 平台admin_签OA_ADMIN_不签scopeTenantId()
    {
        var claims = AuthService.BuildAdminClaims(isPlatformAdmin: true, isTenantAdmin: false, new[] { TenantA }).ToList();
        Assert.Contains(claims, c => c.Type == ClaimTypes.Role && c.Value == AdminAuthorizationService.AdminRoleClaim);
        Assert.DoesNotContain(claims, c => c.Type == "scopeTenantId");
        Assert.DoesNotContain(claims, c => c.Type == "tenantAdmin");
    }

    [Fact]
    public void 租户admin_签tenantAdmin与scopeTenantId_无OA_ADMIN()
    {
        var claims = AuthService.BuildAdminClaims(isPlatformAdmin: false, isTenantAdmin: true, new[] { TenantA }).ToList();
        Assert.DoesNotContain(claims, c => c.Value == AdminAuthorizationService.AdminRoleClaim);
        Assert.Contains(claims, c => c.Type == "tenantAdmin" && c.Value == "1");
        Assert.Contains(claims, c => c.Type == "scopeTenantId" && c.Value == TenantA.ToString());
    }

    // ========== B：租户数据墙 ==========

    [Fact]
    public async STT.Task 租户A_admin_用户列表只见本租户_不见他租户()
    {
        using var ctx = Seed(nameof(租户A_admin_用户列表只见本租户_不见他租户));
        var res = await UserSvc(ctx, ScopeA).GetPagedListAsync(new UserPagedRequest { PageIndex = 1, PageSize = 100 });
        Assert.Equal(200, res.Code);
        Assert.Contains(res.Data!.Items, u => u.Id == TenantAUserId);
        Assert.DoesNotContain(res.Data!.Items, u => u.Id == TenantBUserId);
    }

    [Fact]
    public async STT.Task 平台admin_用户列表见全部租户_回归()
    {
        using var ctx = Seed(nameof(平台admin_用户列表见全部租户_回归));
        var res = await UserSvc(ctx, ScopePlatform).GetPagedListAsync(new UserPagedRequest { PageIndex = 1, PageSize = 100 });
        Assert.Equal(200, res.Code);
        Assert.Contains(res.Data!.Items, u => u.Id == TenantAUserId);
        Assert.Contains(res.Data!.Items, u => u.Id == TenantBUserId);
    }

    [Fact]
    public async STT.Task 租户A_admin_读他租户用户_失败()
    {
        using var ctx = Seed(nameof(租户A_admin_读他租户用户_失败));
        var res = await UserSvc(ctx, ScopeA).GetByIdAsync(TenantBUserId);
        Assert.NotEqual(200, res.Code);
    }

    [Fact]
    public async STT.Task 租户A_admin_重置他租户用户密码_失败且哈希不变()
    {
        using var ctx = Seed(nameof(租户A_admin_重置他租户用户密码_失败且哈希不变));
        var res = await UserSvc(ctx, ScopeA).ResetPasswordAsync(TenantBUserId, "brand-new-pass-123");
        Assert.NotEqual(200, res.Code);

        // 确认 B 用户密码未被改动
        var bUser = await ctx.Set<SysUser>().AsNoTracking().FirstAsync(u => u.FID == TenantBUserId);
        Assert.Equal("hashB", bUser.FPasswordHash);
    }

    [Fact]
    public async STT.Task 平台admin_可重置任意租户密码_回归()
    {
        using var ctx = Seed(nameof(平台admin_可重置任意租户密码_回归));
        var res = await UserSvc(ctx, ScopePlatform).ResetPasswordAsync(TenantBUserId, "brand-new-pass-123");
        Assert.Equal(200, res.Code);
    }

    [Fact]
    public async STT.Task 租户A_admin_给用户授全局admin角色_被拒()
    {
        using var ctx = Seed(nameof(租户A_admin_给用户授全局admin角色_被拒));
        var res = await UserSvc(ctx, ScopeA).UpdateAsync(TenantAAdminUserId, new UpdateUserRequest
        {
            Name = "A管理员",
            Status = 1,
            RoleIds = new List<long> { PlatformAdminRoleId } // 平台 admin 角色 → 越权提权
        });
        Assert.NotEqual(200, res.Code);
    }

    [Fact]
    public async STT.Task 租户A_admin_对全局角色授权_被拒()
    {
        using var ctx = Seed(nameof(租户A_admin_对全局角色授权_被拒));
        var res = await RoleSvc(ctx, ScopeA).AssignPermissionsAsync(PlatformAdminRoleId, new List<long> { 5, 6 });
        Assert.NotEqual(200, res.Code);
    }

    [Fact]
    public async STT.Task 租户A_admin_角色列表不含他租户私有角色()
    {
        using var ctx = Seed(nameof(租户A_admin_角色列表不含他租户私有角色));
        // 追加一个租户B私有角色
        ctx.Set<SysRole>().Add(new SysRole { FID = 20, FName = "租户B管理员", FCode = "TENANT_ADMIN_TB", FScope = SysRoleScope.Tenant, FTenantId = TenantB, FIsAdmin = true });
        await ctx.SaveChangesAsync();

        var res = await RoleSvc(ctx, ScopeA).GetAllAsync();
        Assert.Equal(200, res.Code);
        Assert.Contains(res.Data!, r => r.Id == TenantAAdminRoleId); // 本租户可见
        Assert.Contains(res.Data!, r => r.Id == PlatformAdminRoleId); // 平台共享可见
        Assert.DoesNotContain(res.Data!, r => r.Id == 20);            // 他租户私有不可见
    }

    [Fact]
    public async STT.Task 租户A_admin_组织树只见本租户()
    {
        using var ctx = Seed(nameof(租户A_admin_组织树只见本租户));
        var res = await OrgSvc(ctx, ScopeA).GetTreeAsync();
        Assert.Equal(200, res.Code);
        Assert.Contains(res.Data!, o => o.Id == TenantA);
        Assert.DoesNotContain(res.Data!, o => o.Id == TenantB);
    }

    [Fact]
    public async STT.Task 租户A_admin_查他租户用户岗位_空()
    {
        using var ctx = Seed(nameof(租户A_admin_查他租户用户岗位_空));
        var list = await PosSvc(ctx, ScopeA).GetByUserAsync(TenantBUserId);
        Assert.Empty(list);
    }

    [Fact]
    public async STT.Task 租户A_admin_给岗位分配他租户用户_被拒()
    {
        using var ctx = Seed(nameof(租户A_admin_给岗位分配他租户用户_被拒));
        // 全局目录岗位（无部门关联）
        ctx.Set<SysPosition>().Add(new SysPosition { FID = 500, FName = "通用岗位", FCode = "GP", FUID = "gp" });
        await ctx.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => PosSvc(ctx, ScopeA).AssignUsersAsync(500, new[] { TenantBUserId }));
    }

    // ========== 测试替身 ==========

    private sealed class StubScope : ITenantAdminScopeAccessor
    {
        private readonly TenantDataScope _scope;
        public StubScope(TenantDataScope scope) => _scope = scope;
        public STT.Task<TenantDataScope> ResolveAsync() => STT.Task.FromResult(_scope);
    }

    private sealed class NullHttp : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }

    private sealed class NoopEvents : IEventDispatcher
    {
        public STT.Task PublishAsync<T>(T @event) where T : BusinessEvent => STT.Task.CompletedTask;
    }

    private sealed class NoopChangeLog : IChangeLogService
    {
        public STT.Task LogChangeAsync(string businessType, long businessId, string businessName,
            string operationType, string changeContent, long? operatorId, string? operatorName) => STT.Task.CompletedTask;
        public STT.Task<(List<ChangeLogDto> Items, int Total)> GetPagedListAsync(ChangeLogQueryRequest request)
            => STT.Task.FromResult((new List<ChangeLogDto>(), 0));
        public STT.Task<List<ChangeLogDto>> GetByBusinessAsync(string businessType, long businessId)
            => STT.Task.FromResult(new List<ChangeLogDto>());
        public string CompareAndSerialize<T>(T oldEntity, T newEntity, params string[] excludeProperties) => "";
    }

    private sealed class NoopCodeRule : ICodeRuleService
    {
        public STT.Task<string> GenerateNextCodeAsync(string ruleCode, long? orgId = null) => STT.Task.FromResult($"{ruleCode}0001");
        public STT.Task<List<string>> GenerateBatchCodesAsync(string ruleCode, int count, long? orgId = null) => STT.Task.FromResult(new List<string>());
        public STT.Task<ApiResult<List<CodeRuleDto>>> GetAllRulesAsync() => throw new NotImplementedException();
        public STT.Task<ApiResult<CodeRuleDto>> GetRuleByIdAsync(long id) => throw new NotImplementedException();
        public STT.Task<ApiResult<CodeRuleDto>> UpdateRuleAsync(long id, CodeRuleUpdateDto dto) => throw new NotImplementedException();
        public STT.Task<ApiResult<CodeRuleDto>> CreateRuleAsync(CodeRuleCreateDto dto) => throw new NotImplementedException();
        public STT.Task<ApiResult> DeleteRuleAsync(long id) => throw new NotImplementedException();
        public STT.Task<ApiResult<string>> PreviewCodeAsync(long id) => throw new NotImplementedException();
    }
}
