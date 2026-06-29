using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;

namespace STOTOP.WebAPI.Data.Seeders;

/// <summary>
/// 薪酬管理（Salary）模块版本化迁移
///
/// 设计要点：
/// - 表结构由 EF Core + SchemaAutoSync 自动建立，本 Seeder 仅负责
///   菜单/权限注册、唯一约束与查询索引补齐
/// - 三态幂等：未建表跳过 → 部分建索引补齐 → 全部就绪跳过
/// - SYS迁移历史 由 MigrationRunner 统一登记（F模块=Salary，F版本号=int）
///
/// FID 范围：1600-1619
///   1600  薪酬管理（模块）
///   1601  薪酬总览
///   1602  薪酬档位
///   1603  员工薪酬档案
///   1604  月度工资单
///   1605  我的工资条
///   1606  晋升规则
///   1607  晋升评审
///   1610-1615 操作类功能权限（隐藏）
/// </summary>
public static class SalarySeeder
{
    private const string Module = "Salary";

    public static void Migrate(STOTOPDbContext ctx)
    {
        MigrationRunner.RunMigrations(ctx, Module, new List<MigrationStep>
        {
            new(1, "薪酬模块初始化：菜单权限+唯一约束+查询索引", MigrateV1),
        });
    }

    /// <summary>
    /// V1：注册菜单/权限，并为 SAL 业务表补建唯一约束和查询索引（三态幂等）。
    /// </summary>
    private static void MigrateV1(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        SeedMenusAndPermissions(ctx);
        SeedUniqueIndexes(ctx);
        SeedQueryIndexes(ctx);
    }

    /// <summary>
    /// 注册薪酬模块菜单（FID 1600-1607）与隐藏功能权限（FID 1610-1615）。
    /// 全部以 IF NOT EXISTS 包裹，重复执行幂等。
    /// </summary>
    private static void SeedMenusAndPermissions(STOTOPDbContext ctx)
    {
        ctx.Database.ExecuteSqlRaw(@"
-- === 薪酬模块主菜单 ===
IF NOT EXISTS (SELECT 1 FROM [SYS功能权限] WHERE [F编码]=N'sal:manage' OR [FID] = 1600)
    INSERT INTO [SYS功能权限] ([FID],[F名称],[F编码],[F类型],[F父ID],[F路由],[F组件路径],[F图标],[F排序],[F状态],[F是否可见])
    VALUES (1600, N'薪酬管理', N'sal:manage', N'模块', 0, N'/salary', NULL, N'DollarOutlined', 160, 1, 1);

-- === 薪酬子菜单 ===
IF NOT EXISTS (SELECT 1 FROM [SYS功能权限] WHERE [F编码]=N'sal:dashboard:view' OR [FID] = 1601)
    INSERT INTO [SYS功能权限] ([FID],[F名称],[F编码],[F类型],[F父ID],[F路由],[F组件路径],[F图标],[F排序],[F状态],[F是否可见])
    VALUES (1601, N'薪酬总览', N'sal:dashboard:view', N'菜单', 1600, N'/salary/dashboard', N'salary/SalaryDashboard', NULL, 1, 1, 1);

IF NOT EXISTS (SELECT 1 FROM [SYS功能权限] WHERE [F编码]=N'sal:grades:view' OR [FID] = 1602)
    INSERT INTO [SYS功能权限] ([FID],[F名称],[F编码],[F类型],[F父ID],[F路由],[F组件路径],[F图标],[F排序],[F状态],[F是否可见])
    VALUES (1602, N'薪酬档位', N'sal:grades:view', N'菜单', 1600, N'/salary/grades', N'salary/SalaryGrades', NULL, 2, 1, 1);

IF NOT EXISTS (SELECT 1 FROM [SYS功能权限] WHERE [F编码]=N'sal:archives:view' OR [FID] = 1603)
    INSERT INTO [SYS功能权限] ([FID],[F名称],[F编码],[F类型],[F父ID],[F路由],[F组件路径],[F图标],[F排序],[F状态],[F是否可见])
    VALUES (1603, N'员工薪酬档案', N'sal:archives:view', N'菜单', 1600, N'/salary/archives', N'salary/SalaryArchives', NULL, 3, 1, 1);

IF NOT EXISTS (SELECT 1 FROM [SYS功能权限] WHERE [F编码]=N'sal:payrolls:view' OR [FID] = 1604)
    INSERT INTO [SYS功能权限] ([FID],[F名称],[F编码],[F类型],[F父ID],[F路由],[F组件路径],[F图标],[F排序],[F状态],[F是否可见])
    VALUES (1604, N'月度工资单', N'sal:payrolls:view', N'菜单', 1600, N'/salary/payrolls', N'salary/SalaryPayrolls', NULL, 4, 1, 1);

IF NOT EXISTS (SELECT 1 FROM [SYS功能权限] WHERE [F编码]=N'sal:my-payslip:view' OR [FID] = 1605)
    INSERT INTO [SYS功能权限] ([FID],[F名称],[F编码],[F类型],[F父ID],[F路由],[F组件路径],[F图标],[F排序],[F状态],[F是否可见])
    VALUES (1605, N'我的工资条', N'sal:my-payslip:view', N'菜单', 1600, N'/salary/my-payslip', N'salary/SalaryMyPayslip', NULL, 5, 1, 1);

IF NOT EXISTS (SELECT 1 FROM [SYS功能权限] WHERE [F编码]=N'sal:promotion-rules:view' OR [FID] = 1606)
    INSERT INTO [SYS功能权限] ([FID],[F名称],[F编码],[F类型],[F父ID],[F路由],[F组件路径],[F图标],[F排序],[F状态],[F是否可见])
    VALUES (1606, N'晋升规则', N'sal:promotion-rules:view', N'菜单', 1600, N'/salary/promotion-rules', N'salary/SalaryPromotionRules', NULL, 6, 1, 1);

IF NOT EXISTS (SELECT 1 FROM [SYS功能权限] WHERE [F编码]=N'sal:promotion-reviews:view' OR [FID] = 1607)
    INSERT INTO [SYS功能权限] ([FID],[F名称],[F编码],[F类型],[F父ID],[F路由],[F组件路径],[F图标],[F排序],[F状态],[F是否可见])
    VALUES (1607, N'晋升评审', N'sal:promotion-reviews:view', N'菜单', 1600, N'/salary/promotion-reviews', N'salary/SalaryPromotionReviews', NULL, 7, 1, 1);

-- === 操作类功能权限（隐藏，F类型=功能，F是否可见=0）===
IF NOT EXISTS (SELECT 1 FROM [SYS功能权限] WHERE [F编码]=N'sal.grade.edit' OR [FID] = 1610)
    INSERT INTO [SYS功能权限] ([FID],[F名称],[F编码],[F类型],[F父ID],[F路由],[F组件路径],[F图标],[F排序],[F状态],[F是否可见])
    VALUES (1610, N'编辑薪酬档位', N'sal.grade.edit', N'功能', 1602, NULL, NULL, NULL, 1, 1, 0);

IF NOT EXISTS (SELECT 1 FROM [SYS功能权限] WHERE [F编码]=N'sal.archive.edit' OR [FID] = 1611)
    INSERT INTO [SYS功能权限] ([FID],[F名称],[F编码],[F类型],[F父ID],[F路由],[F组件路径],[F图标],[F排序],[F状态],[F是否可见])
    VALUES (1611, N'编辑薪酬档案', N'sal.archive.edit', N'功能', 1603, NULL, NULL, NULL, 1, 1, 0);

IF NOT EXISTS (SELECT 1 FROM [SYS功能权限] WHERE [F编码]=N'sal.payroll.audit' OR [FID] = 1612)
    INSERT INTO [SYS功能权限] ([FID],[F名称],[F编码],[F类型],[F父ID],[F路由],[F组件路径],[F图标],[F排序],[F状态],[F是否可见])
    VALUES (1612, N'审核工资单', N'sal.payroll.audit', N'功能', 1604, NULL, NULL, NULL, 1, 1, 0);

IF NOT EXISTS (SELECT 1 FROM [SYS功能权限] WHERE [F编码]=N'sal.payroll.release' OR [FID] = 1613)
    INSERT INTO [SYS功能权限] ([FID],[F名称],[F编码],[F类型],[F父ID],[F路由],[F组件路径],[F图标],[F排序],[F状态],[F是否可见])
    VALUES (1613, N'发放工资单', N'sal.payroll.release', N'功能', 1604, NULL, NULL, NULL, 2, 1, 0);

IF NOT EXISTS (SELECT 1 FROM [SYS功能权限] WHERE [F编码]=N'sal.promotion.rule.edit' OR [FID] = 1614)
    INSERT INTO [SYS功能权限] ([FID],[F名称],[F编码],[F类型],[F父ID],[F路由],[F组件路径],[F图标],[F排序],[F状态],[F是否可见])
    VALUES (1614, N'编辑晋升规则', N'sal.promotion.rule.edit', N'功能', 1606, NULL, NULL, NULL, 1, 1, 0);

IF NOT EXISTS (SELECT 1 FROM [SYS功能权限] WHERE [F编码]=N'sal.promotion.review' OR [FID] = 1615)
    INSERT INTO [SYS功能权限] ([FID],[F名称],[F编码],[F类型],[F父ID],[F路由],[F组件路径],[F图标],[F排序],[F状态],[F是否可见])
    VALUES (1615, N'晋升评审操作', N'sal.promotion.review', N'功能', 1607, NULL, NULL, NULL, 1, 1, 0);
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
-- SAL月度工资单：同一组织 + 员工 + 期间 唯一
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'SAL月度工资单')
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_SAL月度工资单_员工_期间' AND object_id = OBJECT_ID(N'[SAL月度工资单]'))
        CREATE UNIQUE INDEX [UQ_SAL月度工资单_员工_期间] ON [SAL月度工资单] ([F组织ID], [F员工ID], [F期间]);
END

-- SAL员工薪酬档案：组织 + 员工 唯一
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'SAL员工薪酬档案')
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_SAL员工薪酬档案_组织_员工' AND object_id = OBJECT_ID(N'[SAL员工薪酬档案]'))
        CREATE UNIQUE INDEX [UQ_SAL员工薪酬档案_组织_员工] ON [SAL员工薪酬档案] ([F组织ID], [F员工ID]);
END

-- SAL薪酬档位：组织 + 档位编码 唯一
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'SAL薪酬档位')
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_SAL薪酬档位_组织_编码' AND object_id = OBJECT_ID(N'[SAL薪酬档位]'))
        CREATE UNIQUE INDEX [UQ_SAL薪酬档位_组织_编码] ON [SAL薪酬档位] ([F组织ID], [F档位编码]);
END

-- SAL晋升规则：组织 + 当前档位 + 目标档位 唯一
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'SAL晋升规则')
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_SAL晋升规则_组织_当前_目标' AND object_id = OBJECT_ID(N'[SAL晋升规则]'))
        CREATE UNIQUE INDEX [UQ_SAL晋升规则_组织_当前_目标] ON [SAL晋升规则] ([F组织ID], [F当前档位ID], [F目标档位ID]);
END

-- SALB分兑换记录：组织 + 员工 + 期间 唯一
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'SALB分兑换记录')
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_SALB分兑换_组织_员工_期间' AND object_id = OBJECT_ID(N'[SALB分兑换记录]'))
        CREATE UNIQUE INDEX [UQ_SALB分兑换_组织_员工_期间] ON [SALB分兑换记录] ([F组织ID], [F员工ID], [F期间]);
END
");
    }

    /// <summary>
    /// 查询索引（非唯一）：覆盖薪酬模块高频查询路径。
    /// </summary>
    private static void SeedQueryIndexes(STOTOPDbContext ctx)
    {
        ctx.Database.ExecuteSqlRaw(@"
-- SAL晋升评审：组织 + 员工 + 状态 复合查询索引
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'SAL晋升评审')
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SAL晋升评审_组织_员工_状态' AND object_id = OBJECT_ID(N'[SAL晋升评审]'))
        CREATE INDEX [IX_SAL晋升评审_组织_员工_状态] ON [SAL晋升评审] ([F组织ID], [F员工ID], [F状态]);
END

-- SAL月度工资单：组织 + 期间 + 状态 复合查询索引
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'SAL月度工资单')
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SAL月度工资单_组织_期间_状态' AND object_id = OBJECT_ID(N'[SAL月度工资单]'))
        CREATE INDEX [IX_SAL月度工资单_组织_期间_状态] ON [SAL月度工资单] ([F组织ID], [F期间], [F状态]);
END
");
    }
}
