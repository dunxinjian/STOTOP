using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Models;
using STOTOP.Module.CardFlow.Entities;
using STOTOP.Module.Express.Entities;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.System.Entities;
using Xunit;

namespace STOTOP.Module.System.Tests;

/// <summary>
/// 阶段1 租户【漏标扫描】门禁：跨全部活跃模块，凡直接持有 F组织ID 组织级业务数据的实体，
/// 都必须实现 ITenantScoped（进 fail-closed 租户硬墙），否则在租户过滤下裸露（漏标）。
/// 新增一张有 F组织ID 却漏标的表即令本用例转红，堵住"以后又漏标"的回归。
/// </summary>
public class TenantLeakScanTests
{
    /// <summary>
    /// 显式白名单：有 F组织ID 却合法地不实现 ITenantScoped 的实体（每条须写明理由）。
    /// 收敛这份名单 = 收敛全覆盖债务。
    /// </summary>
    private static readonly HashSet<Type> Whitelist = new()
    {
        // 平台：用户↔组织桥表，是租户隔离机制自身的输入（成员/可视范围派生依据），非被隔离的业务行。
        typeof(SysUserOrganization),
        // 平台：用户↔角色 RBAC 绑定（FOrgId 可空=可选按组织授权），身份/权限骨架，非被隔离业务行。
        typeof(SysUserRole),
        // 基础设施/待裁决：编号序列水位（FOrgId 可空），是否按租户各自计数取决于 SysCodeRule.FOrgIsolation（scout ambiguous）。
        typeof(SysCodeSequence),
        // 待裁决：CardFlow 编号序列是否按租户重置（scout ambiguous）——随阶段2 序列口径定论。
        typeof(CfNumberSequence),
        // 共享/延后：账套模板（FIsPreset 平台预置 + 租户自建混合），design 明确推迟阶段4 统一处理。
        typeof(FinAccountTemplate),
        // 延后/待裁决：末端驿站主数据（非 BaseEntity；FOrgId 可空——合作驿站无组织、租户映射需业务定论），本阶段不纳入。
        typeof(ExpLastMileStation),
    };

    [Fact]
    public void 漏标扫描_持组织级数据的实体必须挂租户硬墙或在白名单()
    {
        using var ctx = TestDbContextFactory.Create("tenant_leakscan");
        var entityTypes = ctx.Model.GetEntityTypes().ToList();

        // 护栏A（强）：防"某模块未注册→模型缺实体→漏标扫描假阴性全绿"。每个活跃模块的代表实体须在模型里。
        foreach (var marker in TenantTestModules.Markers)
            Assert.True(entityTypes.Any(e => e.ClrType == marker),
                $"模块代表实体 {marker.FullName} 不在模型中——疑似模块未注册，漏标扫描会假阴性");

        // 护栏B（弱备份）：实体总数下限，防大面积模块缺失。
        Assert.True(entityTypes.Count >= 150,
            $"模型实体类型数 {entityTypes.Count} 低于下限 150，疑似模块注册缺失");

        // 不变量：直接持 F组织ID 的实体必须实现 ITenantScoped。排除三类合法非租户情形：
        //   · IStagingRecord —— STG 暂存表，fan-out 明确延后（见 fan-out 提交§待后续），单独跟踪，不阻塞门禁；
        //   · 有 FAccountSetId —— 经账套→租户传递隔离（Finance 去 IOrgScoped、走账套单一真源）；
        //   · 显式白名单 —— 平台身份骨架 / 待裁决。
        var leaks = entityTypes
            .Where(et => !typeof(ITenantScoped).IsAssignableFrom(et.ClrType))
            // 持组织级数据的候选：实现 IOrgScoped/IOrgOwned，或裸持 FOrgId/FOwnerOrgId 属性（后者覆盖不实现接口却带组织列者，如 CfQualityRule）。
            // 兼收 FOwnerOrgId——IOrgOwned 的隔离键是 FOwnerOrgId 而非 FOrgId，只认 FOrgId 会对未来纯 FOwnerOrgId 漏标实体假阴性。
            .Where(et => typeof(IOrgScoped).IsAssignableFrom(et.ClrType)
                      || typeof(IOrgOwned).IsAssignableFrom(et.ClrType)
                      || et.FindProperty("FOrgId") != null
                      || et.FindProperty("FOwnerOrgId") != null)
            // STG 暂存表(IStagingRecord)已在阶段1全覆盖补 ITenantScoped，不再整类豁免——
            // 未来新增未挂租户的 STG 表(带 FOrgId)会被此门禁捕获。
            .Where(et => et.FindProperty("FAccountSetId") == null)
            .Where(et => !Whitelist.Contains(et.ClrType))
            .Select(et => et.ClrType.FullName!)
            .OrderBy(n => n)
            .ToList();

        Assert.True(leaks.Count == 0,
            "以下实体持有 F组织ID 组织级数据却未实现 ITenantScoped（租户漏标）。请补 ITenantScoped + F租户ID 列" +
            "（照 FinAmoebaManualData/CfQualityRule 等补漏做法：实体 + Configuration 映射/索引 + 各模块 prod seeder 加列回填），" +
            "或若确属跨租户共享/平台/账套传递，请加入 Whitelist 并注明理由：\n  " + string.Join("\n  ", leaks));
    }
}
