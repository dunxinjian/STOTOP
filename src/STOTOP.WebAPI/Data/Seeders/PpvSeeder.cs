using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;

namespace STOTOP.WebAPI.Data.Seeders;

/// <summary>
/// PPV（产值薪酬）模块版本化迁移
///
/// 设计要点：
/// - 表结构由 EF Core + SchemaAutoSync 自动建立，本 Seeder 仅负责
///   菜单/权限注册、唯一约束与查询索引补齐
/// - 三态幂等：未建表跳过 → 部分建索引补齐 → 全部就绪跳过
/// - SYS迁移历史 由 MigrationRunner 统一登记（F模块=PPV，F版本号=int）
///
/// FID 范围：1500-1516
///   1500  PPV管理（模块）
///   1501  PPV总览
///   1502  产值模板
///   1503  产值记录
///   1504  月度汇总
///   1505  我的产值
///   1510-1516 操作类功能权限（隐藏）
/// </summary>
public static class PpvSeeder
{
    private const string Module = "PPV";

    public static void Migrate(STOTOPDbContext ctx)
    {
        MigrationRunner.RunMigrations(ctx, Module, new List<MigrationStep>
        {
            new(1, "PPV模块初始化：菜单权限+唯一约束+查询索引", MigrateV1),
        });
    }

    /// <summary>
    /// V1：注册菜单/权限，并为 PPV 业务表补建唯一约束和查询索引（三态幂等）。
    /// </summary>
    private static void MigrateV1(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        SeedMenusAndPermissions(ctx);
        SeedUniqueIndexes(ctx);
        SeedQueryIndexes(ctx);
    }

    /// <summary>
    /// 注册 PPV 模块菜单（FID 1500-1505）与隐藏功能权限（FID 1510-1516）。
    /// 全部以 IF NOT EXISTS 包裹，重复执行幂等。
    /// </summary>
    private static void SeedMenusAndPermissions(STOTOPDbContext ctx)
    {
        ctx.Database.ExecuteSqlRaw(@"
-- === PPV 模块主菜单 ===
IF NOT EXISTS (SELECT 1 FROM [SYS功能权限] WHERE [F编码]=N'ppv:manage' OR [FID] = 1500)
    INSERT INTO [SYS功能权限] ([FID],[F名称],[F编码],[F类型],[F父ID],[F路由],[F组件路径],[F图标],[F排序],[F状态],[F是否可见])
    VALUES (1500, N'PPV管理', N'ppv:manage', N'模块', 0, N'/ppv', NULL, N'FundOutlined', 150, 1, 1);

-- === PPV 子菜单 ===
IF NOT EXISTS (SELECT 1 FROM [SYS功能权限] WHERE [F编码]=N'ppv:dashboard:view' OR [FID] = 1501)
    INSERT INTO [SYS功能权限] ([FID],[F名称],[F编码],[F类型],[F父ID],[F路由],[F组件路径],[F图标],[F排序],[F状态],[F是否可见])
    VALUES (1501, N'PPV总览', N'ppv:dashboard:view', N'菜单', 1500, N'/ppv/dashboard', N'ppv/PpvDashboard', NULL, 1, 1, 1);

IF NOT EXISTS (SELECT 1 FROM [SYS功能权限] WHERE [F编码]=N'ppv:template:view' OR [FID] = 1502)
    INSERT INTO [SYS功能权限] ([FID],[F名称],[F编码],[F类型],[F父ID],[F路由],[F组件路径],[F图标],[F排序],[F状态],[F是否可见])
    VALUES (1502, N'产值模板', N'ppv:template:view', N'菜单', 1500, N'/ppv/templates', N'ppv/PpvTemplates', NULL, 2, 1, 1);

IF NOT EXISTS (SELECT 1 FROM [SYS功能权限] WHERE [F编码]=N'ppv:record:view' OR [FID] = 1503)
    INSERT INTO [SYS功能权限] ([FID],[F名称],[F编码],[F类型],[F父ID],[F路由],[F组件路径],[F图标],[F排序],[F状态],[F是否可见])
    VALUES (1503, N'产值记录', N'ppv:record:view', N'菜单', 1500, N'/ppv/records', N'ppv/PpvRecords', NULL, 3, 1, 1);

IF NOT EXISTS (SELECT 1 FROM [SYS功能权限] WHERE [F编码]=N'ppv:result:view' OR [FID] = 1504)
    INSERT INTO [SYS功能权限] ([FID],[F名称],[F编码],[F类型],[F父ID],[F路由],[F组件路径],[F图标],[F排序],[F状态],[F是否可见])
    VALUES (1504, N'月度汇总', N'ppv:result:view', N'菜单', 1500, N'/ppv/results', N'ppv/PpvResults', NULL, 4, 1, 1);

IF NOT EXISTS (SELECT 1 FROM [SYS功能权限] WHERE [F编码]=N'ppv:my:view' OR [FID] = 1505)
    INSERT INTO [SYS功能权限] ([FID],[F名称],[F编码],[F类型],[F父ID],[F路由],[F组件路径],[F图标],[F排序],[F状态],[F是否可见])
    VALUES (1505, N'我的产值', N'ppv:my:view', N'菜单', 1500, N'/ppv/my-progress', N'ppv/PpvMyProgress', NULL, 5, 1, 1);

-- === 操作类功能权限（隐藏，F类型=功能，F是否可见=0）===
IF NOT EXISTS (SELECT 1 FROM [SYS功能权限] WHERE [F编码]=N'ppv.template.view' OR [FID] = 1510)
    INSERT INTO [SYS功能权限] ([FID],[F名称],[F编码],[F类型],[F父ID],[F路由],[F组件路径],[F图标],[F排序],[F状态],[F是否可见])
    VALUES (1510, N'查看产值模板', N'ppv.template.view', N'功能', 1502, NULL, NULL, NULL, 1, 1, 0);

IF NOT EXISTS (SELECT 1 FROM [SYS功能权限] WHERE [F编码]=N'ppv.template.edit' OR [FID] = 1511)
    INSERT INTO [SYS功能权限] ([FID],[F名称],[F编码],[F类型],[F父ID],[F路由],[F组件路径],[F图标],[F排序],[F状态],[F是否可见])
    VALUES (1511, N'编辑产值模板', N'ppv.template.edit', N'功能', 1502, NULL, NULL, NULL, 2, 1, 0);

IF NOT EXISTS (SELECT 1 FROM [SYS功能权限] WHERE [F编码]=N'ppv.record.view' OR [FID] = 1512)
    INSERT INTO [SYS功能权限] ([FID],[F名称],[F编码],[F类型],[F父ID],[F路由],[F组件路径],[F图标],[F排序],[F状态],[F是否可见])
    VALUES (1512, N'查看产值记录', N'ppv.record.view', N'功能', 1503, NULL, NULL, NULL, 1, 1, 0);

IF NOT EXISTS (SELECT 1 FROM [SYS功能权限] WHERE [F编码]=N'ppv.record.edit' OR [FID] = 1513)
    INSERT INTO [SYS功能权限] ([FID],[F名称],[F编码],[F类型],[F父ID],[F路由],[F组件路径],[F图标],[F排序],[F状态],[F是否可见])
    VALUES (1513, N'编辑产值记录', N'ppv.record.edit', N'功能', 1503, NULL, NULL, NULL, 2, 1, 0);

IF NOT EXISTS (SELECT 1 FROM [SYS功能权限] WHERE [F编码]=N'ppv.record.review' OR [FID] = 1514)
    INSERT INTO [SYS功能权限] ([FID],[F名称],[F编码],[F类型],[F父ID],[F路由],[F组件路径],[F图标],[F排序],[F状态],[F是否可见])
    VALUES (1514, N'审核产值记录', N'ppv.record.review', N'功能', 1503, NULL, NULL, NULL, 3, 1, 0);

IF NOT EXISTS (SELECT 1 FROM [SYS功能权限] WHERE [F编码]=N'ppv.result.view' OR [FID] = 1515)
    INSERT INTO [SYS功能权限] ([FID],[F名称],[F编码],[F类型],[F父ID],[F路由],[F组件路径],[F图标],[F排序],[F状态],[F是否可见])
    VALUES (1515, N'查看月度汇总', N'ppv.result.view', N'功能', 1504, NULL, NULL, NULL, 1, 1, 0);

IF NOT EXISTS (SELECT 1 FROM [SYS功能权限] WHERE [F编码]=N'ppv.result.recalc' OR [FID] = 1516)
    INSERT INTO [SYS功能权限] ([FID],[F名称],[F编码],[F类型],[F父ID],[F路由],[F组件路径],[F图标],[F排序],[F状态],[F是否可见])
    VALUES (1516, N'重新核算', N'ppv.result.recalc', N'功能', 1504, NULL, NULL, NULL, 2, 1, 0);
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
-- PPV月度汇总：同一组织 + 员工 + 期间 唯一
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'PPV月度汇总')
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_PPV月度汇总_员工_期间' AND object_id = OBJECT_ID(N'[PPV月度汇总]'))
        CREATE UNIQUE INDEX [UQ_PPV月度汇总_员工_期间] ON [PPV月度汇总] ([F组织ID], [F员工ID], [F期间]);
END

-- PPV产值模板：组织 + 岗位 + 产值项编码 唯一
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'PPV产值模板')
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_PPV产值模板_组织_岗位_编码' AND object_id = OBJECT_ID(N'[PPV产值模板]'))
        CREATE UNIQUE INDEX [UQ_PPV产值模板_组织_岗位_编码] ON [PPV产值模板] ([F组织ID], [F岗位ID], [F产值项编码]);
END

-- PPV违规记录：组织 + 员工 + 期间 + 违规类型 + 关联单据 唯一
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'PPV违规记录')
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_PPV违规_组织_员工_期间_类型_单据' AND object_id = OBJECT_ID(N'[PPV违规记录]'))
        CREATE UNIQUE INDEX [UQ_PPV违规_组织_员工_期间_类型_单据] ON [PPV违规记录] ([F组织ID], [F员工ID], [F期间], [F违规类型], [F关联单据ID]);
END
");
    }

    /// <summary>
    /// 查询索引（非唯一）：覆盖 PPV 高频查询路径。
    /// </summary>
    private static void SeedQueryIndexes(STOTOPDbContext ctx)
    {
        ctx.Database.ExecuteSqlRaw(@"
-- PPV产值记录：组织 + 员工 + 期间 复合查询索引
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'PPV产值记录')
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PPV产值记录_组织_员工_期间' AND object_id = OBJECT_ID(N'[PPV产值记录]'))
        CREATE INDEX [IX_PPV产值记录_组织_员工_期间] ON [PPV产值记录] ([F组织ID], [F员工ID], [F期间]);
END

-- PPV月度汇总：组织 + 期间 + 状态 复合查询索引
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'PPV月度汇总')
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PPV月度汇总_组织_期间_状态' AND object_id = OBJECT_ID(N'[PPV月度汇总]'))
        CREATE INDEX [IX_PPV月度汇总_组织_期间_状态] ON [PPV月度汇总] ([F组织ID], [F期间], [F状态]);
END
");
    }
}
