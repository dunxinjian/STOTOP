using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Services;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.System.Dtos;
using STOTOP.Module.System.Entities;
using STOTOP.Module.System.Services.Interfaces;

namespace STOTOP.Module.System.Services;

/// <summary>
/// <see cref="IProvisionTenantService"/> 默认实现（R5）。
/// <para>
/// 作用域约定：本服务在 <see cref="Filters.PlatformOnlyAttribute"/> 的平台作用域下被调用——
/// PLT/SYS组织/用户/角色/成员 均非 ITenantScoped，直写安全；对 ITenantScoped 的 SYS任职/SYS数据范围授权，
/// 每行【显式赋 FTenantId=根组织FID】，并用 <see cref="ITenantScopeFactory"/> 收敛读上下文（防非平台作用域下 fail-closed 读空）。
/// </para>
/// 不变量：租户ID = 根组织节点 FID = PLT租户.FID（三者一致，与 MDSTO 单客户口径相同，供冻结中间件/成员/切换统一解析）。
/// <para>
/// ⚠️ stage4C 阻断项（多租户运行时登录接线前必须先解决）：本服务建的“租户私有 admin 角色”FIsAdmin=true，
/// 登录会带 OA_ADMIN claim → 短路所有 [RequirePermission] 拿全量权限码；而 SYS用户/SYS角色/SYS组织架构 等管理类表
/// 非 ITenantScoped、无租户隔离——故租户级 admin 一旦能登录，即可跨租户读写他租户用户/角色（越权）。
/// 当前不可利用（运行时多租户解析属 stage4C，尚未接线，仅 MDSTO 单客户可登录）。
/// 解阻方案（见 design/23 §12）：① RequirePermission/JWT 区分 platform vs tenant admin（tenant admin 不发全量短路，走实授权限码）；
/// ② 给管理类表补租户维度隔离 + UserController/RoleController 租户过滤。切勿在未解此项前接通 stage4C。
/// </para>
/// </summary>
public class ProvisionTenantService : IProvisionTenantService
{
    private readonly STOTOPDbContext _ctx;
    private readonly IScopeGrantService _scopeGrant;
    private readonly ITenantScopeFactory _tenantScope;
    private readonly ILogger<ProvisionTenantService> _logger;

    public ProvisionTenantService(
        STOTOPDbContext ctx,
        IScopeGrantService scopeGrant,
        ITenantScopeFactory tenantScope,
        ILogger<ProvisionTenantService> logger)
    {
        _ctx = ctx;
        _scopeGrant = scopeGrant;
        _tenantScope = tenantScope;
        _logger = logger;
    }

    public async Task<ProvisionTenantResult> ProvisionAsync(ProvisionTenantRequest request)
    {
        Validate(request);
        var rootOrgCode = string.IsNullOrWhiteSpace(request.RootOrgCode) ? request.Code : request.RootOrgCode!.Trim();
        var relational = _ctx.Database.IsRelational();

        // 初始密码在事务外生成：自开事务经 ExecutionStrategy 执行，失败会以全新事务整体重跑 writes——
        // tempPassword/hash 须稳定，保证返回值与落库 hash 一致。
        var tempPassword = GenerateTempPassword();
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(tempPassword);

        ProvisionTenantResult result = null!;

        // ---- 编排 writes（可重复执行：唯一性预检幂等、纯 DB 写；strategy 重试整体重跑）----
        async Task WritesAsync()
        {
            // 唯一性前置校验
            if (await _ctx.Set<PltTenant>().AnyAsync(t => t.FCode == request.Code))
                throw new InvalidOperationException($"租户编号已存在：{request.Code}");
            if (await _ctx.Set<SysUser>().AnyAsync(u => u.FAccount == request.AdminAccount))
                throw new InvalidOperationException($"管理员账号已存在：{request.AdminAccount}");
            if (await _ctx.Set<SysOrganization>().IgnoreQueryFilters().AnyAsync(o => o.FCode == rootOrgCode))
                throw new InvalidOperationException($"组织编码已存在：{rootOrgCode}");

            var orgType = await _ctx.Set<SysOrgType>().FirstOrDefaultAsync(t => t.FKind == request.RootOrgKind)
                ?? throw new InvalidOperationException($"未找到组织类别 {request.RootOrgKind} 对应的组织类型，无法建根节点");

            // 1. 建组织根节点（非 IOrgScoped/非 ITenantScoped，直写安全）
#pragma warning disable CS0618
            var rootOrg = new SysOrganization
            {
                FUID = Guid.NewGuid().ToString("N"),
                FName = request.RootOrgName,
                FCode = rootOrgCode,
                FParentId = 0,
                FTypeId = orgType.FID,
                FKind = orgType.FKind,
                FParentKind = null, // 根节点：无父类别（CK_合法父子 允许根为空）
                FType = orgType.FName,
                FSort = 0,
                FStatus = 1,
                FIsSwitchable = true, // 公司级根强制可切换
            };
#pragma warning restore CS0618
            await _ctx.Set<SysOrganization>().AddAsync(rootOrg);
            await _ctx.SaveChangesAsync();
            var rootId = rootOrg.FID;

            // 2. 物化派生列(F租户ID=自身)+闭包(自反)+范围根
            OrgTreeMaterializer.RebuildAll(_ctx);

            // 3. 建 PLT租户，FID=根组织FID（保不变量；关系库需 IDENTITY_INSERT）
            await InsertPltTenantAsync(request, rootId, relational);

            // 4. 建初始管理员用户（随机密码，BCrypt）
            var adminUser = new SysUser
            {
                FUID = Guid.NewGuid().ToString("N"),
                FName = request.AdminName,
                FAccount = request.AdminAccount,
                FPasswordHash = passwordHash,
                FPhone = request.AdminPhone,
                FStatus = 1,
                FIsPlatformAdmin = false, // 租户管理员绝非平台超管（不得打进 /api/platform/*）
            };
            await _ctx.Set<SysUser>().AddAsync(adminUser);
            await _ctx.SaveChangesAsync();
            var userId = adminUser.FID;

            // 5. 建租户私有 admin 角色（FScope=tenant/FTenantId=根组织FID/FIsAdmin=true）
            //    ⚠️ 见类注释 stage4C 阻断项：FIsAdmin 目前授全量权限码，管理类表未租户隔离 → 接线前须先解。
            var adminRole = new SysRole
            {
                FName = "租户管理员",
                FCode = $"TENANT_ADMIN_{request.Code}",
                FDescription = $"{request.Name} 租户管理员（R5 开通自动创建）",
                FScope = SysRoleScope.Tenant,
                FTenantId = rootId,
                FIsAdmin = true,
                FStatus = 1,
            };
            await _ctx.Set<SysRole>().AddAsync(adminRole);
            await _ctx.SaveChangesAsync();
            var roleId = adminRole.FID;

            // 6. 授角色 + 建成员(已接受) + 主组织(SYS用户组织)
            await _ctx.Set<SysUserRole>().AddAsync(new SysUserRole
            {
                FUserId = userId,
                FRoleId = roleId,
                FOrgId = rootId,
            });
            var member = new SysTenantMember
            {
                FUserId = userId,
                FTenantId = rootId,
                FIsPrimary = true,
                FInviteStatus = 2, // 已接受：自动开通跳过邀请握手
                FJoinedAt = DateTime.Now,
                FStatus = 1,
            };
            await _ctx.Set<SysTenantMember>().AddAsync(member);
            await _ctx.Set<SysUserOrganization>().AddAsync(new SysUserOrganization
            {
                FUserId = userId,
                FOrgId = rootId,
                FIsPrimaryOrg = 1,
                F是否当前 = true,
                FStatus = 1,
            });
            await _ctx.SaveChangesAsync();
            var memberId = member.FID;

            // 7. 主任职(SYS任职, ITenantScoped) + 重算 R8 派生授权
            //    收敛读上下文到新租户（非平台作用域下 fail-closed 读须命中）；写行显式 FTenantId。
            using (_tenantScope.Enter(rootId, "tenant-provision-r5"))
            {
                await _ctx.Set<SysAppointment>().AddAsync(new SysAppointment
                {
                    FTenantId = rootId,
                    FMemberId = memberId,
                    FOrgId = rootId,
                    FIsPrimary = true,
                    FScopeEligible = true, // 喂 R8：主任职可放大 → 派生本租户树 Read 授权
                    FIsCurrent = true,
                    FStatus = 1,
                });
                await _ctx.SaveChangesAsync();

                await _scopeGrant.RecomputeScopeGrantsAsync(userId, rootId);
            }

            result = new ProvisionTenantResult
            {
                TenantId = rootId,
                RootOrgId = rootId,
                AdminUserId = userId,
                AdminRoleId = roleId,
                AdminAccount = request.AdminAccount,
                TempPassword = tempPassword,
            };
        }

        await WithTransactionAsync(relational, WritesAsync);

        _logger.LogInformation("R5 开通租户成功 tenant={TenantId} code={Code} admin={Admin}",
            result.TenantId, request.Code, request.AdminAccount);
        return result;
    }

    /// <summary>把开通 writes 包进事务：关系库自开事务【须经 ExecutionStrategy 执行】——DbContext 启用了
    /// EnableRetryOnFailure，直接 BeginTransaction 会抛 "SqlServerRetryingExecutionStrategy does not support
    /// user-initiated transactions"。已在外层事务则复用；InMemory 退化为直接执行。（同 VoucherService.WithTransactionAsync）</summary>
    private async Task WithTransactionAsync(bool relational, Func<Task> writes)
    {
        if (!relational || _ctx.Database.CurrentTransaction != null)
        {
            await writes();
            return;
        }
        var strategy = _ctx.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _ctx.Database.BeginTransactionAsync();
            try
            {
                await writes();
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        });
    }

    private async Task InsertPltTenantAsync(ProvisionTenantRequest request, long rootId, bool relational)
    {
        if (relational)
        {
            // SqlServer 身份列插入指定 FID 须 IDENTITY_INSERT；EF 不插身份列值，走参数化原始 SQL。
            const string sql = @"
SET IDENTITY_INSERT [PLT租户] ON;
INSERT INTO [PLT租户] ([FID],[F名称],[F编号],[F根组织ID],[F账套绑定模式],[F默认待办渠道],[F套餐ID],[F开通时间],[F到期时间],[F状态],[F创建时间],[F更新时间])
VALUES ({0},{1},{2},{0},{3},{4},{5},{6},{7},{8},GETDATE(),GETDATE());
SET IDENTITY_INSERT [PLT租户] OFF;";
            await _ctx.Database.ExecuteSqlRawAsync(sql,
                rootId, request.Name, request.Code, request.AccountSetBindMode, request.DefaultTodoChannel,
                (object?)request.PlanId ?? DBNull.Value, DateTime.Now,
                (object?)request.ExpireAt ?? DBNull.Value, (int)PltTenantStatus.Trial);
        }
        else
        {
            // InMemory：直接落 FID
            await _ctx.Set<PltTenant>().AddAsync(new PltTenant
            {
                FID = rootId,
                FName = request.Name,
                FCode = request.Code,
                FRootOrgId = rootId,
                FAccountSetBindMode = request.AccountSetBindMode,
                FDefaultTodoChannel = request.DefaultTodoChannel,
                FPlanId = request.PlanId,
                FActivatedAt = DateTime.Now,
                FExpireAt = request.ExpireAt,
                FStatus = (int)PltTenantStatus.Trial,
            });
            await _ctx.SaveChangesAsync();
        }
    }

    private static void Validate(ProvisionTenantRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) throw new InvalidOperationException("租户名称不能为空");
        if (string.IsNullOrWhiteSpace(request.Code)) throw new InvalidOperationException("租户编号不能为空");
        if (string.IsNullOrWhiteSpace(request.RootOrgName)) throw new InvalidOperationException("根组织名称不能为空");
        if (string.IsNullOrWhiteSpace(request.AdminAccount)) throw new InvalidOperationException("管理员账号不能为空");
        if (string.IsNullOrWhiteSpace(request.AdminName)) throw new InvalidOperationException("管理员姓名不能为空");
        if (!OrgTreeMaterializer.IsLegalRootKind(request.RootOrgKind))
            throw new InvalidOperationException($"非法根组织类别：{request.RootOrgKind}（仅允许 0 集团 / 1 区域公司 / 2 网点公司）");
    }

    /// <summary>生成满足常见复杂度（含大小写/数字/符号，长度 14）的一次性初始密码。</summary>
    private static string GenerateTempPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnpqrstuvwxyz";
        const string digit = "23456789";
        const string symbol = "!@#$%&*";
        const string all = upper + lower + digit + symbol;

        var sb = new StringBuilder();
        sb.Append(Pick(upper));
        sb.Append(Pick(lower));
        sb.Append(Pick(digit));
        sb.Append(Pick(symbol));
        for (var i = 0; i < 10; i++) sb.Append(Pick(all));

        // 打乱固定前四位的位置
        var arr = sb.ToString().ToCharArray();
        for (var i = arr.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (arr[i], arr[j]) = (arr[j], arr[i]);
        }
        return new string(arr);

        static char Pick(string pool) => pool[RandomNumberGenerator.GetInt32(pool.Length)];
    }
}
