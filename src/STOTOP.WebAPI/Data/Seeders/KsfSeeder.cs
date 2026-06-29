using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;

namespace STOTOP.WebAPI.Data.Seeders;

/// <summary>
/// KSF（关键成功因素）模块版本化迁移
///
/// 设计要点：
/// - 表结构由 EF Core + SchemaAutoSync 自动建立，本 Seeder 仅负责
///   菜单/权限注册、唯一约束与查询索引补齐
/// - 三态幂等：未建表跳过 → 部分建索引补齐 → 全部就绪跳过
/// - SYS迁移历史 由 MigrationRunner 统一登记（F模块=KSF，F版本号=int）
///
/// FID 范围：1400-1419
///   1400  KSF管理（模块）
///   1401  KSF总览
///   1402  指标库
///   1403  岗位方案
///   1404  核算结果
///   1405  我的KSF
///   1406  员工映射
///   1410-1419 操作类功能权限（隐藏）
/// </summary>
public static class KsfSeeder
{
    private const string Module = "KSF";

    public static void Migrate(STOTOPDbContext ctx)
    {
        MigrationRunner.RunMigrations(ctx, Module, new List<MigrationStep>
        {
            new(1, "KSF模块初始化：菜单权限+唯一约束+查询索引", MigrateV1),
        });
    }

    /// <summary>
    /// V1：注册菜单/权限，并为 KSF 业务表补建唯一约束和查询索引（三态幂等）。
    /// </summary>
    private static void MigrateV1(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        SeedMenusAndPermissions(ctx);
        SeedUniqueIndexes(ctx);
        SeedQueryIndexes(ctx);
    }

    /// <summary>
    /// 注册 KSF 模块菜单（FID 1400-1406）与隐藏功能权限（FID 1410-1419）。
    /// 全部以 IF NOT EXISTS 包裹，重复执行幂等。
    /// </summary>
    private static void SeedMenusAndPermissions(STOTOPDbContext ctx)
    {
        ctx.Database.ExecuteSqlRaw(@"
-- === KSF 模块主菜单 ===
IF NOT EXISTS (SELECT 1 FROM [SYS功能权限] WHERE [F编码]=N'ksf:manage' OR [FID] = 1400)
    INSERT INTO [SYS功能权限] ([FID],[F名称],[F编码],[F类型],[F父ID],[F路由],[F组件路径],[F图标],[F排序],[F状态],[F是否可见])
    VALUES (1400, N'KSF管理', N'ksf:manage', N'模块', 0, N'/ksf', NULL, N'LineChartOutlined', 8, 1, 1);

-- === KSF 子菜单 ===
IF NOT EXISTS (SELECT 1 FROM [SYS功能权限] WHERE [F编码]=N'ksf:dashboard:view' OR [FID] = 1401)
    INSERT INTO [SYS功能权限] ([FID],[F名称],[F编码],[F类型],[F父ID],[F路由],[F组件路径],[F图标],[F排序],[F状态],[F是否可见])
    VALUES (1401, N'KSF总览', N'ksf:dashboard:view', N'菜单', 1400, N'/ksf/dashboard', N'ksf/KsfDashboard', NULL, 1, 1, 1);

IF NOT EXISTS (SELECT 1 FROM [SYS功能权限] WHERE [F编码]=N'ksf:indicator:view' OR [FID] = 1402)
    INSERT INTO [SYS功能权限] ([FID],[F名称],[F编码],[F类型],[F父ID],[F路由],[F组件路径],[F图标],[F排序],[F状态],[F是否可见])
    VALUES (1402, N'指标库', N'ksf:indicator:view', N'菜单', 1400, N'/ksf/indicators', N'ksf/KsfIndicators', NULL, 2, 1, 1);

IF NOT EXISTS (SELECT 1 FROM [SYS功能权限] WHERE [F编码]=N'ksf:plan:view' OR [FID] = 1403)
    INSERT INTO [SYS功能权限] ([FID],[F名称],[F编码],[F类型],[F父ID],[F路由],[F组件路径],[F图标],[F排序],[F状态],[F是否可见])
    VALUES (1403, N'岗位方案', N'ksf:plan:view', N'菜单', 1400, N'/ksf/plans', N'ksf/KsfPlans', NULL, 3, 1, 1);

IF NOT EXISTS (SELECT 1 FROM [SYS功能权限] WHERE [F编码]=N'ksf:result:view' OR [FID] = 1404)
    INSERT INTO [SYS功能权限] ([FID],[F名称],[F编码],[F类型],[F父ID],[F路由],[F组件路径],[F图标],[F排序],[F状态],[F是否可见])
    VALUES (1404, N'核算结果', N'ksf:result:view', N'菜单', 1400, N'/ksf/results', N'ksf/KsfResults', NULL, 4, 1, 1);

IF NOT EXISTS (SELECT 1 FROM [SYS功能权限] WHERE [F编码]=N'ksf:my:view' OR [FID] = 1405)
    INSERT INTO [SYS功能权限] ([FID],[F名称],[F编码],[F类型],[F父ID],[F路由],[F组件路径],[F图标],[F排序],[F状态],[F是否可见])
    VALUES (1405, N'我的KSF', N'ksf:my:view', N'菜单', 1400, N'/ksf/my-progress', N'ksf/KsfMyProgress', NULL, 5, 1, 1);

IF NOT EXISTS (SELECT 1 FROM [SYS功能权限] WHERE [F编码]=N'ksf:mapping:view' OR [FID] = 1406)
    INSERT INTO [SYS功能权限] ([FID],[F名称],[F编码],[F类型],[F父ID],[F路由],[F组件路径],[F图标],[F排序],[F状态],[F是否可见])
    VALUES (1406, N'员工映射', N'ksf:mapping:view', N'菜单', 1400, N'/ksf/mappings', N'ksf/KsfMappings', NULL, 6, 1, 1);

-- === 操作类功能权限（隐藏，F类型=功能，F是否可见=0）===
IF NOT EXISTS (SELECT 1 FROM [SYS功能权限] WHERE [F编码]=N'ksf.indicator.edit' OR [FID] = 1410)
    INSERT INTO [SYS功能权限] ([FID],[F名称],[F编码],[F类型],[F父ID],[F路由],[F组件路径],[F图标],[F排序],[F状态],[F是否可见])
    VALUES (1410, N'指标编辑', N'ksf.indicator.edit', N'功能', 1402, NULL, NULL, NULL, 1, 1, 0);

IF NOT EXISTS (SELECT 1 FROM [SYS功能权限] WHERE [F编码]=N'ksf.plan.edit' OR [FID] = 1411)
    INSERT INTO [SYS功能权限] ([FID],[F名称],[F编码],[F类型],[F父ID],[F路由],[F组件路径],[F图标],[F排序],[F状态],[F是否可见])
    VALUES (1411, N'岗位方案编辑', N'ksf.plan.edit', N'功能', 1403, NULL, NULL, NULL, 1, 1, 0);

IF NOT EXISTS (SELECT 1 FROM [SYS功能权限] WHERE [F编码]=N'ksf.plan.activate' OR [FID] = 1412)
    INSERT INTO [SYS功能权限] ([FID],[F名称],[F编码],[F类型],[F父ID],[F路由],[F组件路径],[F图标],[F排序],[F状态],[F是否可见])
    VALUES (1412, N'岗位方案启用', N'ksf.plan.activate', N'功能', 1403, NULL, NULL, NULL, 2, 1, 0);

IF NOT EXISTS (SELECT 1 FROM [SYS功能权限] WHERE [F编码]=N'ksf.result.recalc' OR [FID] = 1413)
    INSERT INTO [SYS功能权限] ([FID],[F名称],[F编码],[F类型],[F父ID],[F路由],[F组件路径],[F图标],[F排序],[F状态],[F是否可见])
    VALUES (1413, N'核算结果重算', N'ksf.result.recalc', N'功能', 1404, NULL, NULL, NULL, 1, 1, 0);

IF NOT EXISTS (SELECT 1 FROM [SYS功能权限] WHERE [F编码]=N'ksf.mapping.edit' OR [FID] = 1414)
    INSERT INTO [SYS功能权限] ([FID],[F名称],[F编码],[F类型],[F父ID],[F路由],[F组件路径],[F图标],[F排序],[F状态],[F是否可见])
    VALUES (1414, N'员工映射编辑', N'ksf.mapping.edit', N'功能', 1406, NULL, NULL, NULL, 1, 1, 0);
");
    }

    /// <summary>
    /// 唯一约束（三态幂等）：
    /// - 状态①未建表：外层 IF EXISTS 跳过
    /// - 状态②表已建但缺索引：内层 IF NOT EXISTS 创建
    /// - 状态③索引齐备：全部跳过
    /// </summary>
    private static void SeedUniqueIndexes(STOTOPDbContext ctx)
    {
        ctx.Database.ExecuteSqlRaw(@"
-- KSF结果：同一组织 + 员工 + 期间 唯一
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'KSF结果')
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_KSF结果_员工_期间' AND object_id = OBJECT_ID(N'[KSF结果]'))
        CREATE UNIQUE INDEX [UQ_KSF结果_员工_期间] ON [KSF结果] ([F组织ID], [F员工ID], [F期间]);
END

-- KSF岗位方案明细：方案内指标唯一
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'KSF岗位方案明细')
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_KSF方案明细_方案_指标' AND object_id = OBJECT_ID(N'[KSF岗位方案明细]'))
        CREATE UNIQUE INDEX [UQ_KSF方案明细_方案_指标] ON [KSF岗位方案明细] ([F方案ID], [F指标ID]);
END

-- KSF指标定义：组织内编码唯一
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'KSF指标定义')
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_KSF指标_组织_编码' AND object_id = OBJECT_ID(N'[KSF指标定义]'))
        CREATE UNIQUE INDEX [UQ_KSF指标_组织_编码] ON [KSF指标定义] ([F组织ID], [F编码]);
END

-- KSF员工经营单元映射：组织+员工+经营单元+生效起期 唯一
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'KSF员工经营单元映射')
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_KSF员工经营单元_员工_单元_生效期' AND object_id = OBJECT_ID(N'[KSF员工经营单元映射]'))
        CREATE UNIQUE INDEX [UQ_KSF员工经营单元_员工_单元_生效期] ON [KSF员工经营单元映射] ([F组织ID], [F员工ID], [F经营单元ID], [F生效起期]);
END
");
    }

    /// <summary>
    /// 查询索引（非唯一）：覆盖 KSF结果 按组织+期间+状态 的高频查询路径。
    /// </summary>
    private static void SeedQueryIndexes(STOTOPDbContext ctx)
    {
        ctx.Database.ExecuteSqlRaw(@"
-- KSF结果：组织 + 期间 + 状态 复合查询索引
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'KSF结果')
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_KSF结果_组织_期间_状态' AND object_id = OBJECT_ID(N'[KSF结果]'))
        CREATE INDEX [IX_KSF结果_组织_期间_状态] ON [KSF结果] ([F组织ID], [F期间], [F状态]);
END
");
    }
}
