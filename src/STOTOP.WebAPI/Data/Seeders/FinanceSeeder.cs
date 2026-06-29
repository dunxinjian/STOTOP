using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.System.Entities;

namespace STOTOP.WebAPI.Data.Seeders;

public static class FinanceSeeder
{
    private const string Module = "Finance";

    /// <summary>
    /// 统一走 SeederHelper.ExecuteRawSql（纯 ADO，不经 EF 的 String.Format），
    /// SQL 字面量中的大括号（JSON、模板占位符）无需转义。
    /// </summary>
    private static void ExecSql(STOTOPDbContext ctx, string sql)
    {
        SeederHelper.ExecuteRawSql(ctx, sql);
    }

    public static void Migrate(STOTOPDbContext ctx)
    {
        var steps = new List<MigrationStep>
        {
            new(1, "数据库基线 (2026-05-26)", MigrateV1),
            new(2, "清理残留废弃列 (2026-05-26)", MigrateV2),
            new(3, "损益项字段数据迁移 (2026-05-27)", MigrateV3),
            new(4, "指标分区标记收敛为根级 (2026-06-12)", MigrateV4),
            new(5, "删除 FIN科目/科目余额/辅助余额 的 F组织ID 列 (2026-06-16)", MigrateV5),
            new(6, "批次6: 删损益项2废弃列+重灌72项种子 (2026-06-17)", MigrateV6),
            new(7, "批次5-S3: 手工数据加 F期间键 + 回填 'M:'+期间 (2026-06-17)", MigrateV7),
            new(8, "批次5-S6: 删废弃 FIN阿米巴分摊比例 表 (2026-06-17)", MigrateV8),
            new(9, "品牌版科目覆盖：删两账套旧科目重建(保留各账套真实1002/1012) (2026-06-18)", MigrateV9),
            new(10, "品牌版科目4项调整：220301/54010501改名 + 新增50010105 + 删54010502 (2026-06-19)", MigrateV10),
            new(11, "快递行业模板FID3明细=账套2科目快照(排1002/1012子科目)+sourceId口径 (2026-06-19)", MigrateV11),
        };
        MigrationRunner.RunMigrations(ctx, Module, steps);
    }

    private static void MigrateV1(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // FIN账套
        ExecSql(ctx, @"
        SET IDENTITY_INSERT [FIN账套] ON;
        
        IF NOT EXISTS (SELECT 1 FROM [FIN账套] WHERE [FID] = 1)
        
        INSERT INTO [FIN账套] ([FID], [F名称], [F编码], [F法人名称], [F说明], [F是否默认], [F状态], [F排序], [F起始年份], [F起始月份], [F组织ID], [F创建时间], [F更新时间]) VALUES (1, N'石家庄申通2025', N'SJZ2025', N'石家庄申通', N'石家庄申通2025年度账套', 1, 1, 1, 2025, 1, 2, N'2026-04-20 09:08:02.793', N'2026-05-07 11:13:31.148');
        
        IF NOT EXISTS (SELECT 1 FROM [FIN账套] WHERE [FID] = 2)
        
        INSERT INTO [FIN账套] ([FID], [F名称], [F编码], [F法人名称], [F说明], [F是否默认], [F状态], [F排序], [F起始年份], [F起始月份], [F组织ID], [F创建时间], [F更新时间]) VALUES (2, N'太仓美申2025', N'TC2025', N'太仓美申', N'太仓美申2025年度账套', 0, 1, 2, 2025, 1, 192, N'2026-04-20 09:08:02.793', N'2026-04-20 09:08:02.793');
        
        SET IDENTITY_INSERT [FIN账套] OFF;
        
        ");

        // FIN科目模板
        ExecSql(ctx, @"
        SET IDENTITY_INSERT [FIN科目模板] ON;
        
        IF NOT EXISTS (SELECT 1 FROM [FIN科目模板] WHERE [FID] = 1)
        
        INSERT INTO [FIN科目模板] ([FID], [F编码], [F名称], [F说明], [F是否预置], [F启用状态], [F组织ID], [F创建时间], [F更新时间]) VALUES (1, N'SJZ_ST', N'石家庄申通', NULL, 1, 1, 0, N'2026-04-20 09:08:02.090', N'2026-04-20 09:08:02.090');
        
        IF NOT EXISTS (SELECT 1 FROM [FIN科目模板] WHERE [FID] = 2)
        
        INSERT INTO [FIN科目模板] ([FID], [F编码], [F名称], [F说明], [F是否预置], [F启用状态], [F组织ID], [F创建时间], [F更新时间]) VALUES (2, N'TC_MS', N'太仓美申', NULL, 1, 1, 0, N'2026-04-20 09:08:02.090', N'2026-04-20 09:08:02.090');
        
        IF NOT EXISTS (SELECT 1 FROM [FIN科目模板] WHERE [FID] = 3)
        
        INSERT INTO [FIN科目模板] ([FID], [F编码], [F名称], [F说明], [F是否预置], [F启用状态], [F组织ID], [F创建时间], [F更新时间]) VALUES (3, N'express-delivery', N'快递行业标准模板', N'源自石家庄申通2025账套快照', 1, 1, 0, N'2026-05-19 00:41:06.333', N'2026-05-19 00:41:06.333');
        
        SET IDENTITY_INSERT [FIN科目模板] OFF;
        
        ");

        // FIN科目（品牌版，2026-06-18）
        InsertBrandAccounts(ctx);

        // FIN阿米巴损益模板
        ExecSql(ctx, @"
        SET IDENTITY_INSERT [FIN阿米巴损益模板] ON;
        
        IF NOT EXISTS (SELECT 1 FROM [FIN阿米巴损益模板] WHERE [FID] = 3)
        
        INSERT INTO [FIN阿米巴损益模板] ([FID], [F名称], [F描述], [F是否默认], [F创建时间], [F更新时间], [F账套ID]) VALUES (3, N'太仓经营损益模板', NULL, 1, N'2026-05-12 00:44:25.870', N'2026-05-24 15:53:24.332', 2);
        
        SET IDENTITY_INSERT [FIN阿米巴损益模板] OFF;
        
        ");

        // FIN阿米巴损益项（批次6: 72项种子, 与 MigrateV6 共用 ReseedAmoebaTemplate3 守卫重灌）
        ReseedAmoebaTemplate3(ctx);

        // 唯一索引: UQ_FIN凭证_VoucherNo
        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_FIN凭证_VoucherNo' AND object_id = OBJECT_ID('FIN凭证'))
        BEGIN
        CREATE UNIQUE INDEX [UQ_FIN凭证_VoucherNo] ON [FIN凭证] ([F凭证字], [F期间ID], [F账套ID], [F凭证号]);
        END
        ");

    }

    private static void MigrateV2(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // [FIN阿米巴损益模板]: 清除 FTabsJson（先删默认约束再删列）
        SeederHelper.DropColumnSafe(ctx, "FIN阿米巴损益模板", "FTabsJson");

        // [FIN阿米巴损益项]: 清除 F项目类型, F显示级别, F业务方向（先删默认约束再删列）
        SeederHelper.DropColumnSafe(ctx, "FIN阿米巴损益项", "F项目类型");
        SeederHelper.DropColumnSafe(ctx, "FIN阿米巴损益项", "F显示级别");
        SeederHelper.DropColumnSafe(ctx, "FIN阿米巴损益项", "F业务方向");
    }

    private static void MigrateV3(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // 数据迁移：基于 FNodeRole + FDataSource 推导新字段值

        // F值来源映射
        ExecSql(ctx, @"
        UPDATE [FIN阿米巴损益项] SET [F值来源] = CASE 
          WHEN [F数据源] IN ('voucher','billing','estimate','depreciation') THEN 'system'
          WHEN [F数据源] = 'formula' THEN 'formula'
          WHEN [F数据源] = 'manual' THEN 'manual'
          ELSE NULL
        END
        WHERE [F值来源] IS NULL;
        ");

        // F系统数据源映射
        ExecSql(ctx, @"
        UPDATE [FIN阿米巴损益项] SET [F系统数据源] = [F数据源]
        WHERE [F数据源] IN ('voucher','billing','estimate','depreciation')
          AND [F系统数据源] IS NULL;
        ");

        // F项目类别映射 Step 1: 直接映射的类型
        ExecSql(ctx, @"
        UPDATE [FIN阿米巴损益项] SET [F项目类别] = 'section'   WHERE [F节点角色] = 'group'     AND [F项目类别] IS NULL;
        UPDATE [FIN阿米巴损益项] SET [F项目类别] = 'indicator'  WHERE [F节点角色] = 'indicator' AND [F项目类别] IS NULL;
        UPDATE [FIN阿米巴损益项] SET [F项目类别] = 'profit'     WHERE [F节点角色] = 'formula'   AND [F项目类别] IS NULL;
        ");

        // F项目类别映射 Step 1.5: 标记 indicator section
        // 指标分区按设计为"全局唯一、根级"——仅根级（F父ID=0）group 可标记；
        // Tab 内嵌套的指标组（如出港指标/进港指标）是普通分组，不得标记
        ExecSql(ctx, @"
        UPDATE p SET p.[F是否指标分区] = 1
        FROM [FIN阿米巴损益项] p
        WHERE p.[F节点角色] = 'group'
          AND p.[F父ID] = 0
          AND EXISTS (SELECT 1 FROM [FIN阿米巴损益项] c WHERE c.[F父ID] = p.[FID] AND c.[F节点角色] = 'indicator')
          AND p.[F是否指标分区] = 0;
        ");

        // F项目类别映射 Step 2: data 类型需根据父节点名称判断 revenue/cost
        ExecSql(ctx, @"
        UPDATE t SET t.[F项目类别] = CASE
          WHEN p.[F项目名称] LIKE '%收入%' THEN 'revenue'
          ELSE 'cost'
        END
        FROM [FIN阿米巴损益项] t
        INNER JOIN [FIN阿米巴损益项] p ON t.[F父ID] = p.[FID]
        WHERE t.[F节点角色] = 'data' AND t.[F项目类别] IS NULL;
        ");
    }

    private static void MigrateV4(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // 修复 V3 Step 1.5 的过度标记：指标分区按设计为"全局唯一、根级"，
        // V3 把所有含 indicator 子项的 group（如 Tab 内嵌套的出港指标/进港指标）都标记成了
        // 指标分区，导致多期报表只分离其中一个、另一个残留主 Tab，且前端因"已存在指标分区"
        // 无法再创建真正的根级运营指标分区。非根级标记一律回归普通分组。
        ExecSql(ctx, @"
        UPDATE [FIN阿米巴损益项]
        SET [F是否指标分区] = 0
        WHERE [F是否指标分区] = 1 AND [F父ID] <> 0;
        ");
    }

    private static void MigrateV5(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // F组织ID 已从模型移除（账套作用域）。删除既存库的物理列；
        // DropColumnSafe 先删依赖索引/DEFAULT 约束再删列，且 IF EXISTS 列幂等（全新库列本就不存在即跳过）。
        SeederHelper.DropColumnSafe(ctx, "FIN科目", "F组织ID");
        SeederHelper.DropColumnSafe(ctx, "FIN科目余额", "F组织ID");
        SeederHelper.DropColumnSafe(ctx, "FIN辅助核算余额", "F组织ID");
    }

    private static void MigrateV6(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;
        // 批次6: 删 2 个废弃列(已从实体/Config 移除); DropColumnSafe 幂等(IF EXISTS), 全新库列本不存在即跳过
        SeederHelper.DropColumnSafe(ctx, "FIN阿米巴损益项", "F辅助核算过滤Json");
        SeederHelper.DropColumnSafe(ctx, "FIN阿米巴损益项", "F指标方向范围");
        // 存量库重灌损益项种子: V1 内种子对已迁移库不会重跑, 故在此守卫重灌
        ReseedAmoebaTemplate3(ctx);
    }

    /// <summary>
    /// 批次5-S3: FIN阿米巴手工数据 加 F期间键(粒度前缀+期间)，回填存量(全月度)为 'M:'+F期间。
    /// 列幂等补加(IF NOT EXISTS)——dev 下 SchemaAutoSync 可能已在本步前加好，prod(AutoSync 暂存)则由本步加。
    /// 唯一索引保持 F期间 不变(各粒度期间串本就唯一)，不在此动索引(避免管线在回填前用全 NULL 期间键建索引撞 NULL 重复)。
    /// 加列与回填分两个 batch：UPDATE 引用的列须由前一 batch 先建好(SQL Server 延迟名称解析)。
    /// </summary>
    private static void MigrateV7(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        ExecSql(ctx, @"
        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_NAME = N'FIN阿米巴手工数据' AND COLUMN_NAME = N'F期间键')
        ALTER TABLE [FIN阿米巴手工数据] ADD [F期间键] nvarchar(20) NULL;
        ");

        ExecSql(ctx, @"
        UPDATE [FIN阿米巴手工数据] SET [F期间键] = N'M:' + [F期间] WHERE [F期间键] IS NULL;
        ");
    }

    /// <summary>
    /// 批次5-S6: 删废弃的 FIN阿米巴分摊比例 表(旧固定比例分摊，已被件量分摊 F分摊方式/F分摊基数 取代)。
    /// 实体/Config/CRUD/前端/baseline 菜单已删；幂等 DROP(IF EXISTS)，全新库该表本不存在即跳过。
    /// 顺带删存量库残留的"分摊配置"孤儿菜单行(指向已删路由/组件；baseline upsert 不删缺失行，故此处清)。
    /// </summary>
    private static void InsertBrandAccounts(STOTOPDbContext ctx)
    {
        // 品牌版科目(2026-06-18 落库)：两账套品牌版业务科目 + 各账套真实1002/1012；FID 600/700xxx
        ExecSql(ctx, @"
        SET IDENTITY_INSERT [FIN科目] ON;
        -- 账套1 共482科目(含现有1002/1012=75)
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600001)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600001, N'1001', N'库存现金', N'流动资产', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600002)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600002, N'1002', N'银行存款', N'流动资产', N'借', 1, 0, 0, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600003)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600003, N'1012', N'其他货币资金', N'流动资产', N'借', 1, 0, 0, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600004)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600004, N'1101', N'短期投资', N'流动资产', N'借', 1, 0, 0, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600005)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600005, N'1121', N'应收票据', N'流动资产', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600006)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600006, N'1122', N'应收账款', N'流动资产', N'借', 1, 0, 0, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600007)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600007, N'1123', N'预付账款', N'流动资产', N'借', 1, 0, 0, N'supplier', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600008)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600008, N'1131', N'应收股利', N'流动资产', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600009)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600009, N'1132', N'应收利息', N'流动资产', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600010)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600010, N'1221', N'其他应收款', N'流动资产', N'借', 1, 0, 0, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600011)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600011, N'1401', N'材料采购', N'流动资产', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600012)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600012, N'1402', N'在途物资', N'流动资产', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600013)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600013, N'1403', N'原材料', N'流动资产', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600014)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600014, N'1404', N'材料成本差异', N'流动资产', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600015)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600015, N'1405', N'库存商品', N'流动资产', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600016)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600016, N'1406', N'发出商品', N'流动资产', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600017)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600017, N'1407', N'商品进销差价', N'流动资产', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600018)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600018, N'1408', N'委托加工物资', N'流动资产', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600019)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600019, N'1411', N'周转材料', N'流动资产', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600020)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600020, N'1421', N'消耗性生物资产', N'流动资产', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600021)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600021, N'1501', N'待摊费用', N'非流动资产', N'借', 1, 0, 0, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600022)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600022, N'1502', N'长期债券投资减值准备', N'非流动资产', N'贷', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600023)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600023, N'1511', N'长期股权投资', N'非流动资产', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600024)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600024, N'1512', N'长期股权投资减值准备', N'非流动资产', N'贷', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600025)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600025, N'1521', N'投资性房地产', N'非流动资产', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600026)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600026, N'1522', N'投资性房地产累计折旧', N'非流动资产', N'贷', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600027)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600027, N'1523', N'投资性房地产减值准备', N'非流动资产', N'贷', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600028)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600028, N'1526', N'投资性房地产清理', N'非流动资产', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600029)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600029, N'1531', N'长期应收款', N'非流动资产', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600030)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600030, N'1601', N'固定资产', N'非流动资产', N'借', 1, 0, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600031)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600031, N'1602', N'累计折旧', N'非流动资产', N'贷', 1, 0, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600032)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600032, N'1603', N'固定资产减值准备', N'非流动资产', N'贷', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600033)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600033, N'1604', N'在建工程', N'非流动资产', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600034)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600034, N'1605', N'工程物资', N'非流动资产', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600035)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600035, N'1606', N'固定资产清理', N'非流动资产', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600036)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600036, N'1621', N'生产性生物资产', N'非流动资产', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600037)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600037, N'1622', N'生产性生物资产累计折旧', N'非流动资产', N'贷', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600038)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600038, N'1623', N'生产性生物资产减值准备', N'非流动资产', N'贷', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600039)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600039, N'1701', N'无形资产', N'非流动资产', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600040)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600040, N'1702', N'累计摊销', N'非流动资产', N'贷', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600041)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600041, N'1703', N'无形资产减值准备', N'非流动资产', N'贷', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600042)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600042, N'1706', N'无形资产清理', N'非流动资产', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600043)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600043, N'1801', N'长期待摊费用', N'非流动资产', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600044)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600044, N'1901', N'待处理财产损溢', N'非流动资产', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600045)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600045, N'2001', N'短期借款', N'流动负债', N'贷', 1, 0, 0, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600046)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600046, N'2201', N'应付票据', N'流动负债', N'贷', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600047)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600047, N'2202', N'应付账款', N'流动负债', N'贷', 1, 0, 0, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600048)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600048, N'2203', N'预收账款', N'流动负债', N'贷', 1, 0, 0, N'customer', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600049)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600049, N'2211', N'应付职工薪酬', N'流动负债', N'贷', 1, 0, 0, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600050)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600050, N'2221', N'应交税费', N'流动负债', N'贷', 1, 0, 0, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600051)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600051, N'2231', N'应付利息', N'流动负债', N'贷', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600052)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600052, N'2232', N'应付利润', N'流动负债', N'贷', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600053)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600053, N'2241', N'其他应付款', N'流动负债', N'贷', 1, 0, 0, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600054)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600054, N'2401', N'递延收益', N'非流动负债', N'贷', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600055)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600055, N'2501', N'长期借款', N'非流动负债', N'贷', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600056)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600056, N'2701', N'长期应付款', N'非流动负债', N'贷', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600057)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600057, N'3001', N'实收资本', N'所有者权益', N'贷', 1, 0, 0, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600058)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600058, N'3002', N'资本公积', N'所有者权益', N'贷', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600059)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600059, N'3101', N'盈余公积', N'所有者权益', N'贷', 1, 0, 0, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600060)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600060, N'3103', N'本年利润', N'所有者权益', N'贷', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600061)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600061, N'3104', N'利润分配', N'所有者权益', N'贷', 1, 0, 0, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600062)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600062, N'4001', N'生产成本', N'成本', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600063)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600063, N'4101', N'制造费用', N'成本', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600064)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600064, N'4201', N'劳务成本', N'成本', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600065)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600065, N'4301', N'研发支出', N'成本', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600066)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600066, N'4401', N'工程施工', N'成本', N'借', 1, 0, 0, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600067)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600067, N'4402', N'工程结算', N'成本', N'贷', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600068)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600068, N'4403', N'机械作业', N'成本', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600069)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600069, N'5001', N'主营业务收入', N'营业收入', N'贷', 1, 0, 0, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600070)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600070, N'5051', N'其他业务收入', N'营业收入', N'贷', 1, 0, 0, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600071)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600071, N'5101', N'公允价值变动损益', N'其他收益', N'贷', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600072)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600072, N'5111', N'投资损益', N'其他收益', N'贷', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600073)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600073, N'5121', N'资产处置损益', N'其他收益', N'贷', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600074)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600074, N'5151', N'其他收益', N'其他收益', N'贷', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600075)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600075, N'5301', N'营业外收入', N'其他收益', N'贷', 1, 0, 0, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600076)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600076, N'5401', N'主营业务成本', N'营业成本', N'借', 1, 0, 0, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600077)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600077, N'5402', N'其他业务成本', N'营业成本', N'借', 1, 0, 0, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600078)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600078, N'5403', N'税金及附加', N'营业税金及附加', N'借', 1, 0, 0, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600079)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600079, N'5601', N'销售费用', N'期间费用', N'借', 1, 0, 0, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600080)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600080, N'5602', N'管理费用', N'期间费用', N'借', 1, 0, 0, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600081)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600081, N'5603', N'财务费用', N'期间费用', N'借', 1, 0, 0, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600082)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600082, N'5701', N'资产减值损失', N'其他损失', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600083)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600083, N'5711', N'营业外支出', N'其他损失', N'借', 1, 0, 0, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600084)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600084, N'5801', N'所得税费用', N'所得税费用', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600085)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600085, N'5901', N'以前年度损益调整', N'以前年度损益调整', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600086)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600086, N'100201', N'中行对公-友谊北支行', N'流动资产', N'借', 2, 600002, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600087)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600087, N'100202', N'中行对公-机场路支行', N'流动资产', N'借', 2, 600002, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600088)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600088, N'100203', N'工行对公-长安支行', N'流动资产', N'借', 2, 600002, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600089)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600089, N'100204', N'邮储对公-裕东支行', N'流动资产', N'借', 2, 600002, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600090)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600090, N'100205', N'农行对公-广安支行', N'流动资产', N'借', 2, 600002, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600091)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600091, N'100206', N'建行-张彦', N'流动资产', N'借', 2, 600002, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600092)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600092, N'100207', N'农行-张彦', N'流动资产', N'借', 2, 600002, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600093)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600093, N'100208', N'支付宝-张彦', N'流动资产', N'借', 2, 600002, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600094)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600094, N'100209', N'支付宝-对公', N'流动资产', N'借', 2, 600002, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600095)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600095, N'100210', N'中行-名门', N'流动资产', N'借', 2, 600002, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600096)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600096, N'100211', N'中行-鼎坚', N'流动资产', N'借', 2, 600002, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600097)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600097, N'100212', N'中行-博雅', N'流动资产', N'借', 2, 600002, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600098)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600098, N'100213', N'中行-友谊', N'流动资产', N'借', 2, 600002, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600099)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600099, N'100214', N'中行-谊北', N'流动资产', N'借', 2, 600002, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600100)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600100, N'100215', N'中行-天佑', N'流动资产', N'借', 2, 600002, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600101)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600101, N'100216', N'中行-新华', N'流动资产', N'借', 2, 600002, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600102)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600102, N'100217', N'中行-长兴', N'流动资产', N'借', 2, 600002, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600103)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600103, N'100218', N'中行-柳源', N'流动资产', N'借', 2, 600002, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600104)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600104, N'100219', N'中行-银都', N'流动资产', N'借', 2, 600002, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600105)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600105, N'100220', N'中行-建设', N'流动资产', N'借', 2, 600002, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600106)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600106, N'100221', N'中行-民生', N'流动资产', N'借', 2, 600002, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600107)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600107, N'100222', N'中行-红旗', N'流动资产', N'借', 2, 600002, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600108)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600108, N'100223', N'中行-汇华', N'流动资产', N'借', 2, 600002, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600109)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600109, N'100224', N'中行-鑫科', N'流动资产', N'借', 2, 600002, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600110)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600110, N'100225', N'中行-瑞府', N'流动资产', N'借', 2, 600002, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600111)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600111, N'100226', N'中行-翟营', N'流动资产', N'借', 2, 600002, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600112)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600112, N'100227', N'中行-东开', N'流动资产', N'借', 2, 600002, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600113)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600113, N'100228', N'中行-高新', N'流动资产', N'借', 2, 600002, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600114)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600114, N'100229', N'中行-鹿泉', N'流动资产', N'借', 2, 600002, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600115)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600115, N'100230', N'中行-凌透', N'流动资产', N'借', 2, 600002, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600116)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600116, N'100231', N'中行-西营', N'流动资产', N'借', 2, 600002, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600117)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600117, N'100232', N'中行-建华', N'流动资产', N'借', 2, 600002, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600118)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600118, N'100233', N'中行-平安', N'流动资产', N'借', 2, 600002, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600119)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600119, N'100234', N'中行-正定', N'流动资产', N'借', 2, 600002, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600120)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600120, N'100235', N'中行-宁晋', N'流动资产', N'借', 2, 600002, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600121)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600121, N'100236', N'中行-栾城', N'流动资产', N'借', 2, 600002, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600122)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600122, N'100237', N'中行-时光', N'流动资产', N'借', 2, 600002, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600123)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600123, N'100238', N'中行-炼油厂', N'流动资产', N'借', 2, 600002, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600124)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600124, N'100239', N'兴业-藁城', N'流动资产', N'借', 2, 600002, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600125)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600125, N'100240', N'兴业-炼油厂', N'流动资产', N'借', 2, 600002, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600126)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600126, N'100241', N'中行-藁定', N'流动资产', N'借', 2, 600002, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600127)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600127, N'101201', N'申通总部-石家庄', N'流动资产', N'借', 2, 600003, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600128)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600128, N'101202', N'申通总部-名门', N'流动资产', N'借', 2, 600003, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600129)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600129, N'101203', N'申通总部-鼎坚', N'流动资产', N'借', 2, 600003, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600130)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600130, N'101204', N'申通总部-博雅', N'流动资产', N'借', 2, 600003, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600131)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600131, N'101205', N'申通总部-友谊', N'流动资产', N'借', 2, 600003, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600132)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600132, N'101206', N'申通总部-谊北', N'流动资产', N'借', 2, 600003, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600133)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600133, N'101207', N'申通总部-天佑', N'流动资产', N'借', 2, 600003, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600134)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600134, N'101208', N'申通总部-新华', N'流动资产', N'借', 2, 600003, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600135)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600135, N'101209', N'申通总部-长兴', N'流动资产', N'借', 2, 600003, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600136)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600136, N'101210', N'申通总部-柳源', N'流动资产', N'借', 2, 600003, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600137)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600137, N'101211', N'申通总部-银都', N'流动资产', N'借', 2, 600003, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600138)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600138, N'101212', N'申通总部-建设', N'流动资产', N'借', 2, 600003, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600139)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600139, N'101213', N'申通总部-民生', N'流动资产', N'借', 2, 600003, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600140)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600140, N'101214', N'申通总部-红旗', N'流动资产', N'借', 2, 600003, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600141)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600141, N'101215', N'申通总部-汇华', N'流动资产', N'借', 2, 600003, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600142)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600142, N'101216', N'申通总部-鑫科', N'流动资产', N'借', 2, 600003, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600143)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600143, N'101217', N'申通总部-瑞府', N'流动资产', N'借', 2, 600003, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600144)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600144, N'101218', N'申通总部-翟营', N'流动资产', N'借', 2, 600003, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600145)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600145, N'101219', N'申通总部-东开', N'流动资产', N'借', 2, 600003, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600146)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600146, N'101220', N'申通总部-高新', N'流动资产', N'借', 2, 600003, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600147)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600147, N'101221', N'申通总部-鹿泉', N'流动资产', N'借', 2, 600003, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600148)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600148, N'101222', N'申通总部-凌透', N'流动资产', N'借', 2, 600003, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600149)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600149, N'101223', N'申通总部-西营', N'流动资产', N'借', 2, 600003, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600150)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600150, N'101224', N'申通总部-建华', N'流动资产', N'借', 2, 600003, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600151)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600151, N'101225', N'申通总部-平安', N'流动资产', N'借', 2, 600003, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600152)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600152, N'101226', N'申通总部-正定', N'流动资产', N'借', 2, 600003, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600153)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600153, N'101227', N'申通总部-宁晋', N'流动资产', N'借', 2, 600003, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600154)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600154, N'101228', N'申通总部-栾城', N'流动资产', N'借', 2, 600003, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600155)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600155, N'101229', N'申通总部-时光', N'流动资产', N'借', 2, 600003, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600156)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600156, N'101230', N'申通总部-藁城', N'流动资产', N'借', 2, 600003, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600157)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600157, N'101231', N'申通总部-炼油厂', N'流动资产', N'借', 2, 600003, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600158)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600158, N'101232', N'申通总部-藁定', N'流动资产', N'借', 2, 600003, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600159)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600159, N'110101', N'股票', N'流动资产', N'借', 2, 600004, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600160)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600160, N'110102', N'债券', N'流动资产', N'借', 2, 600004, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600161)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600161, N'110103', N'基金', N'流动资产', N'借', 2, 600004, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600162)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600162, N'112201', N'月结账款', N'流动资产', N'借', 2, 600006, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600163)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600163, N'112202', N'客户账款', N'流动资产', N'借', 2, 600006, 1, N'customer', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600164)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600164, N'112210', N'其他账款', N'流动资产', N'借', 2, 600006, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600165)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600165, N'112301', N'供应商预付', N'流动资产', N'借', 2, 600007, 1, N'supplier', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600166)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600166, N'112302', N'预付驿站款', N'流动资产', N'借', 2, 600007, 1, N'supplier', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600167)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600167, N'122101', N'借款', N'流动资产', N'借', 2, 600010, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600168)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600168, N'122102', N'押金', N'流动资产', N'借', 2, 600010, 0, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600169)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600169, N'122103', N'李青', N'流动资产', N'借', 2, 600010, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600170)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600170, N'122104', N'保证金', N'流动资产', N'借', 2, 600010, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600171)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600171, N'122105', N'其他项目垫付款', N'流动资产', N'借', 2, 600010, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600172)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600172, N'122106', N'美申集团', N'流动资产', N'借', 2, 600010, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600173)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600173, N'150101', N'房租费', N'非流动资产', N'借', 2, 600021, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600174)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600174, N'150102', N'物业费', N'非流动资产', N'借', 2, 600021, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600175)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600175, N'150103', N'停车费', N'非流动资产', N'借', 2, 600021, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600176)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600176, N'150104', N'信息系统费用', N'非流动资产', N'借', 2, 600021, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600177)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600177, N'150105', N'采暖费', N'非流动资产', N'借', 2, 600021, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600178)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600178, N'150106', N'宽带费', N'非流动资产', N'借', 2, 600021, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600179)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600179, N'200101', N'河北银行', N'流动负债', N'贷', 2, 600045, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600180)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600180, N'200102', N'邮储银行', N'流动负债', N'贷', 2, 600045, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600181)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600181, N'200103', N'申通总部', N'流动负债', N'贷', 2, 600045, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600182)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600182, N'200104', N'工行长安支行', N'流动负债', N'贷', 2, 600045, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600183)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600183, N'200105', N'其他', N'流动负债', N'贷', 2, 600045, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600184)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600184, N'220201', N'总部应付', N'流动负债', N'贷', 2, 600047, 1, N'express_brand', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600185)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600185, N'220202', N'供应商应付', N'流动负债', N'贷', 2, 600047, 1, N'supplier', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600186)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600186, N'220299', N'其他应付', N'流动负债', N'贷', 2, 600047, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600187)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600187, N'220301', N'预购面单预收', N'流动负债', N'贷', 2, 600048, 1, N'customer', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600188)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600188, N'220399', N'其他预收', N'流动负债', N'贷', 2, 600048, 1, N'customer', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600189)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600189, N'221101', N'工资【正式工】', N'流动负债', N'贷', 2, 600049, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600190)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600190, N'221102', N'工资【临时工】', N'流动负债', N'贷', 2, 600049, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600191)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600191, N'221103', N'职工保险', N'流动负债', N'贷', 2, 600049, 0, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600192)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600192, N'221105', N'住房公积金', N'流动负债', N'贷', 2, 600049, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600193)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600193, N'221106', N'工会经费', N'流动负债', N'贷', 2, 600049, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600194)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600194, N'221107', N'职工教育经费', N'流动负债', N'贷', 2, 600049, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600195)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600195, N'221108', N'非货币性福利', N'流动负债', N'贷', 2, 600049, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600196)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600196, N'221109', N'辞退福利', N'流动负债', N'贷', 2, 600049, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600197)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600197, N'221199', N'其他', N'流动负债', N'贷', 2, 600049, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600198)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600198, N'222101', N'应交增值税', N'流动负债', N'贷', 2, 600050, 0, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600199)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600199, N'222102', N'未交增值税', N'流动负债', N'贷', 2, 600050, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600200)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600200, N'222103', N'预交增值税', N'流动负债', N'借', 2, 600050, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600201)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600201, N'222104', N'待抵扣进项税额', N'流动负债', N'借', 2, 600050, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600202)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600202, N'222105', N'待认证进项税额', N'流动负债', N'借', 2, 600050, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600203)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600203, N'222106', N'待转销项税额', N'流动负债', N'贷', 2, 600050, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600204)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600204, N'222107', N'增值税留抵税额', N'流动负债', N'借', 2, 600050, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600205)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600205, N'222108', N'简易计税', N'流动负债', N'贷', 2, 600050, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600206)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600206, N'222109', N'转出金融商品增值税', N'流动负债', N'贷', 2, 600050, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600207)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600207, N'222110', N'代扣代交增值税', N'流动负债', N'贷', 2, 600050, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600208)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600208, N'222111', N'消费税', N'流动负债', N'贷', 2, 600050, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600209)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600209, N'222112', N'城市维护建设税', N'流动负债', N'贷', 2, 600050, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600210)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600210, N'222113', N'教育费附加', N'流动负债', N'贷', 2, 600050, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600211)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600211, N'222114', N'地方教育附加', N'流动负债', N'贷', 2, 600050, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600212)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600212, N'222115', N'土地增值税', N'流动负债', N'贷', 2, 600050, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600213)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600213, N'222116', N'土地使用税', N'流动负债', N'贷', 2, 600050, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600214)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600214, N'222117', N'房产税', N'流动负债', N'贷', 2, 600050, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600215)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600215, N'222118', N'车船税', N'流动负债', N'贷', 2, 600050, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600216)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600216, N'222119', N'资源税', N'流动负债', N'贷', 2, 600050, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600217)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600217, N'222120', N'矿产资源补偿费', N'流动负债', N'贷', 2, 600050, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600218)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600218, N'222121', N'排污费', N'流动负债', N'贷', 2, 600050, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600219)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600219, N'222124', N'企业所得税', N'流动负债', N'贷', 2, 600050, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600220)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600220, N'222125', N'代扣代交所得税', N'流动负债', N'贷', 2, 600050, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600221)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600221, N'222199', N'其他', N'流动负债', N'贷', 2, 600050, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600222)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600222, N'224101', N'代收货款', N'流动负债', N'贷', 2, 600053, 1, N'customer', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600223)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600223, N'224102', N'风险保证金', N'流动负债', N'贷', 2, 600053, 1, N'supplier', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600224)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600224, N'224104', N'三轮车保险赔偿款', N'流动负债', N'贷', 2, 600053, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600225)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600225, N'224106', N'生育津贴', N'流动负债', N'贷', 2, 600053, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600226)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600226, N'224107', N'代理点保证金', N'流动负债', N'贷', 2, 600053, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600227)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600227, N'224110', N'客户待退款', N'流动负债', N'贷', 2, 600053, 1, N'customer', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600228)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600228, N'300101', N'李青', N'所有者权益', N'贷', 2, 600057, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600229)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600229, N'300102', N'敦新建', N'所有者权益', N'贷', 2, 600057, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600230)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600230, N'310101', N'法定盈余公积', N'所有者权益', N'贷', 2, 600059, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600231)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600231, N'310102', N'任意盈余公积', N'所有者权益', N'贷', 2, 600059, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600232)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600232, N'310401', N'提取法定盈余公积', N'所有者权益', N'借', 2, 600061, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600233)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600233, N'310402', N'提取任意盈余公积', N'所有者权益', N'借', 2, 600061, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600234)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600234, N'310403', N'应付利润', N'所有者权益', N'借', 2, 600061, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600235)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600235, N'310404', N'转作资本的利润', N'所有者权益', N'借', 2, 600061, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600236)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600236, N'310405', N'未分配利润', N'所有者权益', N'贷', 2, 600061, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600237)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600237, N'440101', N'合同成本', N'成本', N'借', 2, 600066, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600238)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600238, N'440102', N'间接费用', N'成本', N'借', 2, 600066, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600239)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600239, N'440103', N'合同毛利', N'成本', N'借', 2, 600066, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600240)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600240, N'500101', N'出港收入', N'营业收入', N'贷', 2, 600069, 0, N'outlet,business_direction,express_brand,business_unit,customer,project', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600241)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600241, N'500102', N'派费', N'营业收入', N'贷', 2, 600069, 0, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600242)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600242, N'500103', N'操作', N'营业收入', N'贷', 2, 600069, 0, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600243)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600243, N'500104', N'增值服务', N'营业收入', N'贷', 2, 600069, 0, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600244)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600244, N'500105', N'政策', N'营业收入', N'贷', 2, 600069, 0, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600245)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600245, N'500106', N'考核激励', N'营业收入', N'贷', 2, 600069, 0, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600246)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600246, N'500107', N'客服', N'营业收入', N'贷', 2, 600069, 0, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600247)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600247, N'505101', N'食堂收入', N'营业收入', N'贷', 2, 600070, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600248)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600248, N'505103', N'其他收入', N'营业收入', N'贷', 2, 600070, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600249)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600249, N'530101', N'盘盈收益', N'其他收益', N'贷', 2, 600075, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600250)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600250, N'530102', N'非流动资产处置净收益', N'其他收益', N'贷', 2, 600075, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600251)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600251, N'530103', N'无法支付的应付账款', N'其他收益', N'贷', 2, 600075, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600252)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600252, N'530107', N'政府补助', N'其他收益', N'贷', 2, 600075, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600253)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600253, N'530108', N'违约金收益', N'其他收益', N'贷', 2, 600075, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600254)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600254, N'530109', N'捐赠收益', N'其他收益', N'贷', 2, 600075, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600255)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600255, N'530199', N'其他', N'其他收益', N'贷', 2, 600075, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600256)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600256, N'540101', N'面单', N'营业成本', N'借', 2, 600076, 0, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600257)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600257, N'540102', N'派费', N'营业成本', N'借', 2, 600076, 0, N'outlet,business_direction,express_brand,business_unit,supplier,employee', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600258)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600258, N'540103', N'中转', N'营业成本', N'借', 2, 600076, 0, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600259)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600259, N'540104', N'操作', N'营业成本', N'借', 2, 600076, 0, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600260)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600260, N'540105', N'增值服务', N'营业成本', N'借', 2, 600076, 0, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600261)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600261, N'540106', N'考核罚款', N'营业成本', N'借', 2, 600076, 0, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600262)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600262, N'540107', N'客服', N'营业成本', N'借', 2, 600076, 0, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600263)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600263, N'540108', N'物料', N'营业成本', N'借', 2, 600076, 0, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600264)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600264, N'540109', N'运输', N'营业成本', N'借', 2, 600076, 0, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600265)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600265, N'540201', N'食堂采购成本', N'营业成本', N'借', 2, 600077, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600266)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600266, N'540299', N'其他成本', N'营业成本', N'借', 2, 600077, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600267)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600267, N'540301', N'消费税', N'营业税金及附加', N'借', 2, 600078, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600268)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600268, N'540302', N'城建税', N'营业税金及附加', N'借', 2, 600078, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600269)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600269, N'540303', N'教育费附加', N'营业税金及附加', N'借', 2, 600078, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600270)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600270, N'540304', N'地方教育附加', N'营业税金及附加', N'借', 2, 600078, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600271)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600271, N'540305', N'土地增值税', N'营业税金及附加', N'借', 2, 600078, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600272)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600272, N'540306', N'土地使用税', N'营业税金及附加', N'借', 2, 600078, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600273)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600273, N'540307', N'房产税', N'营业税金及附加', N'借', 2, 600078, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600274)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600274, N'540308', N'车船税', N'营业税金及附加', N'借', 2, 600078, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600275)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600275, N'540309', N'印花税', N'营业税金及附加', N'借', 2, 600078, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600276)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600276, N'540310', N'资源税', N'营业税金及附加', N'借', 2, 600078, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600277)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600277, N'540311', N'矿产资源补偿费', N'营业税金及附加', N'借', 2, 600078, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600278)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600278, N'540312', N'排污费', N'营业税金及附加', N'借', 2, 600078, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600279)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600279, N'540399', N'其他', N'营业税金及附加', N'借', 2, 600078, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600280)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600280, N'560101', N'薪酬', N'期间费用', N'借', 2, 600079, 0, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600281)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600281, N'560102', N'折旧', N'期间费用', N'借', 2, 600079, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600282)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600282, N'560103', N'摊销', N'期间费用', N'借', 2, 600079, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600283)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600283, N'560104', N'租金', N'期间费用', N'借', 2, 600079, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600284)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600284, N'560105', N'水电费', N'期间费用', N'借', 2, 600079, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600285)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600285, N'560106', N'物管费', N'期间费用', N'借', 2, 600079, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600286)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600286, N'560107', N'维修费', N'期间费用', N'借', 2, 600079, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600287)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600287, N'560108', N'办公费', N'期间费用', N'借', 2, 600079, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600288)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600288, N'560109', N'通讯费', N'期间费用', N'借', 2, 600079, 0, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600289)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600289, N'560110', N'交通费', N'期间费用', N'借', 2, 600079, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600290)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600290, N'560111', N'差旅费', N'期间费用', N'借', 2, 600079, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600291)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600291, N'560112', N'会务费', N'期间费用', N'借', 2, 600079, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600292)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600292, N'560113', N'物流费', N'期间费用', N'借', 2, 600079, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600293)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600293, N'560114', N'招待费', N'期间费用', N'借', 2, 600079, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600294)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600294, N'560115', N'宣传费', N'期间费用', N'借', 2, 600079, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600295)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600295, N'560116', N'装修费', N'期间费用', N'借', 2, 600079, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600296)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600296, N'560117', N'监控费', N'期间费用', N'借', 2, 600079, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600297)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600297, N'560118', N'信息系统费用', N'期间费用', N'借', 2, 600079, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600298)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600298, N'560119', N'招聘费', N'期间费用', N'借', 2, 600079, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600299)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600299, N'560120', N'培训费', N'期间费用', N'借', 2, 600079, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600300)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600300, N'560121', N'外包服务费', N'期间费用', N'借', 2, 600079, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600301)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600301, N'560122', N'事故赔偿费', N'期间费用', N'借', 2, 600079, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600302)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600302, N'560123', N'客户返款', N'期间费用', N'借', 2, 600079, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600303)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600303, N'560124', N'业务用设备', N'期间费用', N'借', 2, 600079, 0, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600304)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600304, N'560131', N'快递柜', N'期间费用', N'借', 2, 600079, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600305)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600305, N'560199', N'其他', N'期间费用', N'借', 2, 600079, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600306)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600306, N'560201', N'薪酬', N'期间费用', N'借', 2, 600080, 0, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600307)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600307, N'560202', N'折旧', N'期间费用', N'借', 2, 600080, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600308)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600308, N'560203', N'摊销', N'期间费用', N'借', 2, 600080, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600309)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600309, N'560204', N'租金', N'期间费用', N'借', 2, 600080, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600310)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600310, N'560205', N'水电费', N'期间费用', N'借', 2, 600080, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600311)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600311, N'560206', N'物管费', N'期间费用', N'借', 2, 600080, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600312)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600312, N'560207', N'维修费', N'期间费用', N'借', 2, 600080, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600313)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600313, N'560208', N'办公费', N'期间费用', N'借', 2, 600080, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600314)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600314, N'560209', N'通讯费', N'期间费用', N'借', 2, 600080, 0, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600315)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600315, N'560210', N'交通费', N'期间费用', N'借', 2, 600080, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600316)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600316, N'560211', N'差旅费', N'期间费用', N'借', 2, 600080, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600317)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600317, N'560212', N'会务费', N'期间费用', N'借', 2, 600080, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600318)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600318, N'560213', N'装修费', N'期间费用', N'借', 2, 600080, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600319)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600319, N'560214', N'招待费', N'期间费用', N'借', 2, 600080, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600320)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600320, N'560215', N'宣传费', N'期间费用', N'借', 2, 600080, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600321)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600321, N'560216', N'咨询费', N'期间费用', N'借', 2, 600080, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600322)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600322, N'560217', N'信息系统费用', N'期间费用', N'借', 2, 600080, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600323)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600323, N'560218', N'监控费', N'期间费用', N'借', 2, 600080, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600324)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600324, N'560219', N'招聘费', N'期间费用', N'借', 2, 600080, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600325)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600325, N'560220', N'培训费', N'期间费用', N'借', 2, 600080, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600326)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600326, N'560221', N'开办费', N'期间费用', N'借', 2, 600080, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600327)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600327, N'560222', N'研发费', N'期间费用', N'借', 2, 600080, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600328)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600328, N'560223', N'外包服务费', N'期间费用', N'借', 2, 600080, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600329)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600329, N'560299', N'其他', N'期间费用', N'借', 2, 600080, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600330)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600330, N'560301', N'利息', N'期间费用', N'借', 2, 600081, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600331)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600331, N'560302', N'手续费', N'期间费用', N'借', 2, 600081, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600332)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600332, N'560303', N'汇兑损益', N'期间费用', N'借', 2, 600081, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600333)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600333, N'560399', N'其他', N'期间费用', N'借', 2, 600081, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600334)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600334, N'571101', N'盘亏毁损', N'其他损失', N'借', 2, 600083, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600335)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600335, N'571102', N'非流动资产处置净损失', N'其他损失', N'借', 2, 600083, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600336)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600336, N'571103', N'坏账损失', N'其他损失', N'借', 2, 600083, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600337)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600337, N'571104', N'长期债券损失', N'其他损失', N'借', 2, 600083, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600338)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600338, N'571105', N'长期股权损失', N'其他损失', N'借', 2, 600083, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600339)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600339, N'571106', N'不可抗力损失', N'其他损失', N'借', 2, 600083, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600340)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600340, N'571107', N'税收滞纳金', N'其他损失', N'借', 2, 600083, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600341)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600341, N'571108', N'违约金损失', N'其他损失', N'借', 2, 600083, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600342)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600342, N'571109', N'捐赠支出', N'其他损失', N'借', 2, 600083, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600343)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600343, N'571110', N'诉讼赔偿款', N'其他损失', N'借', 2, 600083, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600344)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600344, N'571199', N'其他', N'其他损失', N'借', 2, 600083, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600345)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600345, N'12210201', N'房租押金', N'流动资产', N'借', 3, 600168, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600346)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600346, N'12210210', N'其他押金', N'流动资产', N'借', 3, 600168, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600347)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600347, N'22110301', N'社会保险', N'流动负债', N'贷', 3, 600191, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600348)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600348, N'22110302', N'商业保险', N'流动负债', N'贷', 3, 600191, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600349)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600349, N'22210101', N'进项税额', N'流动负债', N'借', 3, 600198, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600350)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600350, N'22210102', N'销项税额抵减', N'流动负债', N'借', 3, 600198, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600351)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600351, N'22210103', N'已交税金', N'流动负债', N'借', 3, 600198, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600352)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600352, N'22210104', N'转出未交增值税', N'流动负债', N'借', 3, 600198, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600353)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600353, N'22210105', N'减免税款', N'流动负债', N'借', 3, 600198, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600354)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600354, N'22210106', N'出口抵内销', N'流动负债', N'借', 3, 600198, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600355)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600355, N'22210107', N'销项税额', N'流动负债', N'贷', 3, 600198, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600356)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600356, N'22210108', N'出口退税', N'流动负债', N'贷', 3, 600198, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600357)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600357, N'22210109', N'进项税额转出', N'流动负债', N'贷', 3, 600198, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600358)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600358, N'22210110', N'转出多交增值税', N'流动负债', N'贷', 3, 600198, 1, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600359)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600359, N'50010101', N'现付收入', N'营业收入', N'贷', 3, 600240, 1, N'outlet,business_direction,express_brand,business_unit,customer,project', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600360)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600360, N'50010102', N'月结收入', N'营业收入', N'贷', 3, 600240, 1, N'outlet,business_direction,express_brand,business_unit,customer,project', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600361)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600361, N'50010103', N'到付收入', N'营业收入', N'贷', 3, 600240, 1, N'outlet,business_direction,express_brand,business_unit,customer,project', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600362)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600362, N'50010104', N'总部平台单', N'营业收入', N'贷', 3, 600240, 1, N'outlet,business_direction,express_brand,business_unit,customer,project', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600363)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600363, N'50010201', N'基础派费', N'营业收入', N'贷', 3, 600241, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600364)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600364, N'50010202', N'补贴派费', N'营业收入', N'贷', 3, 600241, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600365)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600365, N'50010203', N'大货派费', N'营业收入', N'贷', 3, 600241, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600366)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600366, N'50010204', N'周期派费', N'营业收入', N'贷', 3, 600241, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600367)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600367, N'50010205', N'调整派费', N'营业收入', N'贷', 3, 600241, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600368)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600368, N'50010206', N'按需派费', N'营业收入', N'贷', 3, 600241, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600369)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600369, N'50010207', N'扶持派费', N'营业收入', N'贷', 3, 600241, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600370)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600370, N'50010208', N'小件员权益', N'营业收入', N'贷', 3, 600241, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600371)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600371, N'50010209', N'考核派费', N'营业收入', N'贷', 3, 600241, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600372)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600372, N'50010210', N'VIP专享派费', N'营业收入', N'贷', 3, 600241, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600373)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600373, N'50010211', N'政策互惠', N'营业收入', N'贷', 3, 600241, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600374)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600374, N'50010301', N'退件操作费', N'营业收入', N'贷', 3, 600242, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600375)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600375, N'50010302', N'其他操作费', N'营业收入', N'贷', 3, 600242, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600376)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600376, N'50010401', N'网点线路跑车', N'营业收入', N'贷', 3, 600243, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600377)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600377, N'50010501', N'政策返利', N'营业收入', N'贷', 3, 600244, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600378)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600378, N'50010502', N'政策考核', N'营业收入', N'贷', 3, 600244, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600379)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600379, N'50010601', N'质量考核', N'营业收入', N'贷', 3, 600245, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600380)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600380, N'50010602', N'时效考核', N'营业收入', N'贷', 3, 600245, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600381)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600381, N'50010603', N'操作规范考核', N'营业收入', N'贷', 3, 600245, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600382)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600382, N'50010604', N'客服类考核', N'营业收入', N'贷', 3, 600245, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600383)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600383, N'50010605', N'车辆考核', N'营业收入', N'贷', 3, 600245, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600384)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600384, N'50010606', N'网管类考核', N'营业收入', N'贷', 3, 600245, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600385)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600385, N'50010607', N'工单考核', N'营业收入', N'贷', 3, 600245, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600386)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600386, N'50010608', N'签收率考核', N'营业收入', N'贷', 3, 600245, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600387)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600387, N'50010609', N'时效件投诉考核', N'营业收入', N'贷', 3, 600245, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600388)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600388, N'50010610', N'虚假问题件考核', N'营业收入', N'贷', 3, 600245, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600389)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600389, N'50010611', N'末端类考核', N'营业收入', N'贷', 3, 600245, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600390)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600390, N'50010612', N'省区综合考核', N'营业收入', N'贷', 3, 600245, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600391)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600391, N'50010613', N'管控类', N'营业收入', N'贷', 3, 600245, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600392)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600392, N'50010614', N'KPI激励', N'营业收入', N'贷', 3, 600245, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600393)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600393, N'50010615', N'扶持基金', N'营业收入', N'贷', 3, 600245, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600394)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600394, N'50010701', N'客服受款', N'营业收入', N'贷', 3, 600246, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600395)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600395, N'50010702', N'三件私了', N'营业收入', N'贷', 3, 600246, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600396)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600396, N'54010101', N'面单费', N'营业成本', N'借', 3, 600256, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600397)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600397, N'54010201', N'基础派费', N'营业成本', N'借', 3, 600257, 1, N'outlet,business_direction,express_brand,business_unit,supplier,employee', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600398)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600398, N'54010202', N'补贴派费', N'营业成本', N'借', 3, 600257, 1, N'outlet,business_direction,express_brand,business_unit,supplier,employee', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600399)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600399, N'54010203', N'大货派费', N'营业成本', N'借', 3, 600257, 1, N'outlet,business_direction,express_brand,business_unit,supplier,employee', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600400)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600400, N'54010204', N'周期派费', N'营业成本', N'借', 3, 600257, 1, N'outlet,business_direction,express_brand,business_unit,supplier,employee', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600401)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600401, N'54010205', N'调整派费', N'营业成本', N'借', 3, 600257, 1, N'outlet,business_direction,express_brand,business_unit,supplier,employee', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600402)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600402, N'54010206', N'按需派费', N'营业成本', N'借', 3, 600257, 1, N'outlet,business_direction,express_brand,business_unit,supplier,employee', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600403)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600403, N'54010207', N'扶持派费', N'营业成本', N'借', 3, 600257, 1, N'outlet,business_direction,express_brand,business_unit,supplier,employee', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600404)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600404, N'54010208', N'小件员权益', N'营业成本', N'借', 3, 600257, 1, N'outlet,business_direction,express_brand,business_unit,supplier,employee', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600405)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600405, N'54010209', N'考核派费', N'营业成本', N'借', 3, 600257, 1, N'outlet,business_direction,express_brand,business_unit,supplier,employee', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600406)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600406, N'54010210', N'代派费', N'营业成本', N'借', 3, 600257, 1, N'outlet,business_direction,express_brand,business_unit,supplier,employee', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600407)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600407, N'54010211', N'承包区派费', N'营业成本', N'借', 3, 600257, 1, N'outlet,business_direction,express_brand,business_unit,supplier,employee', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600408)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600408, N'54010212', N'驿站费用', N'营业成本', N'借', 3, 600257, 1, N'outlet,business_direction,express_brand,business_unit,supplier,employee', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600409)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600409, N'54010301', N'中转费', N'营业成本', N'借', 3, 600258, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600410)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600410, N'54010302', N'中转加收', N'营业成本', N'借', 3, 600258, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600411)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600411, N'54010303', N'中转考核', N'营业成本', N'借', 3, 600258, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600412)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600412, N'54010304', N'全网出港费', N'营业成本', N'借', 3, 600258, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600413)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600413, N'54010305', N'其他中转费', N'营业成本', N'借', 3, 600258, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600414)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600414, N'54010401', N'中心集包费', N'营业成本', N'借', 3, 600259, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600415)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600415, N'54010402', N'三方集包费', N'营业成本', N'借', 3, 600259, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600416)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600416, N'54010403', N'退件操作费', N'营业成本', N'借', 3, 600259, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600417)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600417, N'54010404', N'其他操作费', N'营业成本', N'借', 3, 600259, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600418)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600418, N'54010405', N'装卸费', N'营业成本', N'借', 3, 600259, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600419)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600419, N'54010501', N'经营支持服务费', N'营业成本', N'借', 3, 600260, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600420)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600420, N'54010502', N'网点赋能', N'营业成本', N'借', 3, 600260, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600421)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600421, N'54010503', N'网格仓服务费', N'营业成本', N'借', 3, 600260, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600422)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600422, N'54010504', N'智橙网服务费', N'营业成本', N'借', 3, 600260, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600423)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600423, N'54010505', N'信息技术服务费', N'营业成本', N'借', 3, 600260, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600424)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600424, N'54010601', N'质量考核', N'营业成本', N'借', 3, 600261, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600425)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600425, N'54010602', N'时效考核', N'营业成本', N'借', 3, 600261, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600426)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600426, N'54010603', N'操作规范考核', N'营业成本', N'借', 3, 600261, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600427)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600427, N'54010604', N'客服类考核', N'营业成本', N'借', 3, 600261, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600428)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600428, N'54010605', N'车辆考核', N'营业成本', N'借', 3, 600261, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600429)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600429, N'54010606', N'网管类考核', N'营业成本', N'借', 3, 600261, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600430)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600430, N'54010607', N'工单考核', N'营业成本', N'借', 3, 600261, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600431)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600431, N'54010608', N'签收率考核', N'营业成本', N'借', 3, 600261, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600432)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600432, N'54010609', N'时效件投诉考核', N'营业成本', N'借', 3, 600261, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600433)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600433, N'54010610', N'虚假问题件考核', N'营业成本', N'借', 3, 600261, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600434)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600434, N'54010611', N'末端类考核', N'营业成本', N'借', 3, 600261, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600435)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600435, N'54010612', N'省区综合考核', N'营业成本', N'借', 3, 600261, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600436)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600436, N'54010613', N'管控类罚款', N'营业成本', N'借', 3, 600261, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600437)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600437, N'54010701', N'客服赔款', N'营业成本', N'借', 3, 600262, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600438)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600438, N'54010702', N'三件私了', N'营业成本', N'借', 3, 600262, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600439)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600439, N'54010801', N'辅料', N'营业成本', N'借', 3, 600263, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600440)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600440, N'54010802', N'环保袋', N'营业成本', N'借', 3, 600263, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600441)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600441, N'54010901', N'燃油费', N'营业成本', N'借', 3, 600264, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600442)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600442, N'54010902', N'过路费', N'营业成本', N'借', 3, 600264, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600443)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600443, N'54010903', N'维修费', N'营业成本', N'借', 3, 600264, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600444)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600444, N'54010904', N'违章罚款', N'营业成本', N'借', 3, 600264, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600445)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600445, N'54010905', N'保险费', N'营业成本', N'借', 3, 600264, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600446)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600446, N'54010906', N'检车费', N'营业成本', N'借', 3, 600264, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600447)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600447, N'54010907', N'进门/停车费', N'营业成本', N'借', 3, 600264, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600448)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600448, N'54010908', N'租车费用', N'营业成本', N'借', 3, 600264, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600449)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600449, N'54010909', N'新能源车电费', N'营业成本', N'借', 3, 600264, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600450)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600450, N'54010910', N'购置税', N'营业成本', N'借', 3, 600264, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600451)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600451, N'54010911', N'汽运线路费', N'营业成本', N'借', 3, 600264, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600452)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600452, N'54010912', N'车辆折旧', N'营业成本', N'借', 3, 600264, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600453)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600453, N'54010913', N'其他费用', N'营业成本', N'借', 3, 600264, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600454)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600454, N'56010101', N'工资【正式工】', N'期间费用', N'借', 3, 600280, 0, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600455)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600455, N'56010102', N'工资【临时工】', N'期间费用', N'借', 3, 600280, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600456)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600456, N'56010103', N'奖金', N'期间费用', N'借', 3, 600280, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600457)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600457, N'56010104', N'福利', N'期间费用', N'借', 3, 600280, 0, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600458)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600458, N'56010901', N'电话费', N'期间费用', N'借', 3, 600288, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600459)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600459, N'56010902', N'宽带费', N'期间费用', N'借', 3, 600288, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600460)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600460, N'56012401', N'蓝牙秤', N'期间费用', N'借', 3, 600303, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600461)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600461, N'56012402', N'巴枪', N'期间费用', N'借', 3, 600303, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600462)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600462, N'56012403', N'热敏打印机', N'期间费用', N'借', 3, 600303, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600463)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600463, N'56020101', N'工资【正式工】', N'期间费用', N'借', 3, 600306, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600464)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600464, N'56020102', N'工资【临时工】', N'期间费用', N'借', 3, 600306, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600465)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600465, N'56020103', N'奖金', N'期间费用', N'借', 3, 600306, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600466)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600466, N'56020104', N'福利', N'期间费用', N'借', 3, 600306, 0, NULL, NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600467)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600467, N'56020901', N'电话费', N'期间费用', N'借', 3, 600314, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600468)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600468, N'56020902', N'宽带费', N'期间费用', N'借', 3, 600314, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600469)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600469, N'5601010101', N'业务员', N'期间费用', N'借', 4, 600454, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600470)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600470, N'5601010102', N'客服', N'期间费用', N'借', 4, 600454, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600471)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600471, N'5601010103', N'操作', N'期间费用', N'借', 4, 600454, 1, N'department,business_direction', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600472)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600472, N'5601010104', N'司机', N'期间费用', N'借', 4, 600454, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600473)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600473, N'5601010105', N'业务管理', N'期间费用', N'借', 4, 600454, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600474)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600474, N'5601010106', N'其他', N'期间费用', N'借', 4, 600454, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600475)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600475, N'5601010401', N'社会保险', N'期间费用', N'借', 4, 600457, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600476)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600476, N'5601010402', N'商业保险', N'期间费用', N'借', 4, 600457, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600477)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600477, N'5601010403', N'工伤补助', N'期间费用', N'借', 4, 600457, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600478)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600478, N'5601010499', N'其他福利', N'期间费用', N'借', 4, 600457, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600479)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600479, N'5602010401', N'社会保险', N'期间费用', N'借', 4, 600466, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600480)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600480, N'5602010402', N'商业保险', N'期间费用', N'借', 4, 600466, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600481)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600481, N'5602010403', N'工伤补助', N'期间费用', N'借', 4, 600466, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 600482)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (600482, N'5602010499', N'其他福利', N'期间费用', N'借', 4, 600466, 1, N'department', NULL, NULL, 1, 1, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        -- 账套2 共427科目(含现有1002/1012=20)
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700001)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700001, N'1001', N'库存现金', N'流动资产', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700002)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700002, N'1002', N'银行存款', N'流动资产', N'借', 1, 0, 0, N'', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700003)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700003, N'1012', N'其他货币资金', N'流动资产', N'借', 1, 0, 0, N'', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700004)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700004, N'1101', N'短期投资', N'流动资产', N'借', 1, 0, 0, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700005)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700005, N'1121', N'应收票据', N'流动资产', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700006)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700006, N'1122', N'应收账款', N'流动资产', N'借', 1, 0, 0, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700007)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700007, N'1123', N'预付账款', N'流动资产', N'借', 1, 0, 0, N'supplier', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700008)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700008, N'1131', N'应收股利', N'流动资产', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700009)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700009, N'1132', N'应收利息', N'流动资产', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700010)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700010, N'1221', N'其他应收款', N'流动资产', N'借', 1, 0, 0, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700011)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700011, N'1401', N'材料采购', N'流动资产', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700012)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700012, N'1402', N'在途物资', N'流动资产', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700013)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700013, N'1403', N'原材料', N'流动资产', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700014)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700014, N'1404', N'材料成本差异', N'流动资产', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700015)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700015, N'1405', N'库存商品', N'流动资产', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700016)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700016, N'1406', N'发出商品', N'流动资产', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700017)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700017, N'1407', N'商品进销差价', N'流动资产', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700018)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700018, N'1408', N'委托加工物资', N'流动资产', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700019)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700019, N'1411', N'周转材料', N'流动资产', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700020)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700020, N'1421', N'消耗性生物资产', N'流动资产', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700021)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700021, N'1501', N'待摊费用', N'非流动资产', N'借', 1, 0, 0, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700022)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700022, N'1502', N'长期债券投资减值准备', N'非流动资产', N'贷', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700023)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700023, N'1511', N'长期股权投资', N'非流动资产', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700024)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700024, N'1512', N'长期股权投资减值准备', N'非流动资产', N'贷', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700025)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700025, N'1521', N'投资性房地产', N'非流动资产', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700026)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700026, N'1522', N'投资性房地产累计折旧', N'非流动资产', N'贷', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700027)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700027, N'1523', N'投资性房地产减值准备', N'非流动资产', N'贷', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700028)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700028, N'1526', N'投资性房地产清理', N'非流动资产', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700029)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700029, N'1531', N'长期应收款', N'非流动资产', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700030)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700030, N'1601', N'固定资产', N'非流动资产', N'借', 1, 0, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700031)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700031, N'1602', N'累计折旧', N'非流动资产', N'贷', 1, 0, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700032)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700032, N'1603', N'固定资产减值准备', N'非流动资产', N'贷', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700033)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700033, N'1604', N'在建工程', N'非流动资产', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700034)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700034, N'1605', N'工程物资', N'非流动资产', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700035)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700035, N'1606', N'固定资产清理', N'非流动资产', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700036)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700036, N'1621', N'生产性生物资产', N'非流动资产', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700037)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700037, N'1622', N'生产性生物资产累计折旧', N'非流动资产', N'贷', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700038)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700038, N'1623', N'生产性生物资产减值准备', N'非流动资产', N'贷', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700039)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700039, N'1701', N'无形资产', N'非流动资产', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700040)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700040, N'1702', N'累计摊销', N'非流动资产', N'贷', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700041)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700041, N'1703', N'无形资产减值准备', N'非流动资产', N'贷', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700042)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700042, N'1706', N'无形资产清理', N'非流动资产', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700043)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700043, N'1801', N'长期待摊费用', N'非流动资产', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700044)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700044, N'1901', N'待处理财产损溢', N'非流动资产', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700045)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700045, N'2001', N'短期借款', N'流动负债', N'贷', 1, 0, 0, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700046)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700046, N'2201', N'应付票据', N'流动负债', N'贷', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700047)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700047, N'2202', N'应付账款', N'流动负债', N'贷', 1, 0, 0, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700048)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700048, N'2203', N'预收账款', N'流动负债', N'贷', 1, 0, 0, N'customer', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700049)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700049, N'2211', N'应付职工薪酬', N'流动负债', N'贷', 1, 0, 0, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700050)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700050, N'2221', N'应交税费', N'流动负债', N'贷', 1, 0, 0, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700051)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700051, N'2231', N'应付利息', N'流动负债', N'贷', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700052)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700052, N'2232', N'应付利润', N'流动负债', N'贷', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700053)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700053, N'2241', N'其他应付款', N'流动负债', N'贷', 1, 0, 0, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700054)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700054, N'2401', N'递延收益', N'非流动负债', N'贷', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700055)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700055, N'2501', N'长期借款', N'非流动负债', N'贷', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700056)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700056, N'2701', N'长期应付款', N'非流动负债', N'贷', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700057)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700057, N'3001', N'实收资本', N'所有者权益', N'贷', 1, 0, 0, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700058)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700058, N'3002', N'资本公积', N'所有者权益', N'贷', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700059)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700059, N'3101', N'盈余公积', N'所有者权益', N'贷', 1, 0, 0, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700060)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700060, N'3103', N'本年利润', N'所有者权益', N'贷', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700061)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700061, N'3104', N'利润分配', N'所有者权益', N'贷', 1, 0, 0, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700062)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700062, N'4001', N'生产成本', N'成本', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700063)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700063, N'4101', N'制造费用', N'成本', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700064)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700064, N'4201', N'劳务成本', N'成本', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700065)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700065, N'4301', N'研发支出', N'成本', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700066)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700066, N'4401', N'工程施工', N'成本', N'借', 1, 0, 0, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700067)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700067, N'4402', N'工程结算', N'成本', N'贷', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700068)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700068, N'4403', N'机械作业', N'成本', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700069)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700069, N'5001', N'主营业务收入', N'营业收入', N'贷', 1, 0, 0, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700070)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700070, N'5051', N'其他业务收入', N'营业收入', N'贷', 1, 0, 0, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700071)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700071, N'5101', N'公允价值变动损益', N'其他收益', N'贷', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700072)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700072, N'5111', N'投资损益', N'其他收益', N'贷', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700073)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700073, N'5121', N'资产处置损益', N'其他收益', N'贷', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700074)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700074, N'5151', N'其他收益', N'其他收益', N'贷', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700075)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700075, N'5301', N'营业外收入', N'其他收益', N'贷', 1, 0, 0, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700076)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700076, N'5401', N'主营业务成本', N'营业成本', N'借', 1, 0, 0, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700077)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700077, N'5402', N'其他业务成本', N'营业成本', N'借', 1, 0, 0, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700078)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700078, N'5403', N'税金及附加', N'营业税金及附加', N'借', 1, 0, 0, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700079)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700079, N'5601', N'销售费用', N'期间费用', N'借', 1, 0, 0, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700080)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700080, N'5602', N'管理费用', N'期间费用', N'借', 1, 0, 0, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700081)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700081, N'5603', N'财务费用', N'期间费用', N'借', 1, 0, 0, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700082)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700082, N'5701', N'资产减值损失', N'其他损失', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700083)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700083, N'5711', N'营业外支出', N'其他损失', N'借', 1, 0, 0, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700084)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700084, N'5801', N'所得税费用', N'所得税费用', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700085)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700085, N'5901', N'以前年度损益调整', N'以前年度损益调整', N'借', 1, 0, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700086)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700086, N'100201', N'中国银行太仓支行', N'流动资产', N'借', 2, 700002, 1, N'', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700087)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700087, N'100203', N'工行-敦新建', N'流动资产', N'借', 2, 700002, 1, N'', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700088)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700088, N'100204', N'浏河建鑫', N'流动资产', N'借', 2, 700002, 1, N'', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700089)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700089, N'100205', N'沙溪美鑫', N'流动资产', N'借', 2, 700002, 1, N'', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700090)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700090, N'100206', N'韵科-中行', N'流动资产', N'借', 2, 700002, 1, N'', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700091)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700091, N'100207', N'韵科-建行', N'流动资产', N'借', 2, 700002, 1, N'', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700092)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700092, N'100208', N'韵达-瑞予宏', N'流动资产', N'借', 2, 700002, 1, N'', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700093)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700093, N'100209', N'支付宝-对公', N'流动资产', N'借', 2, 700002, 0, N'', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700094)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700094, N'100210', N'极兔傲速', N'流动资产', N'借', 2, 700002, 1, N'', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700095)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700095, N'101201', N'申通城区', N'流动资产', N'借', 2, 700003, 1, N'', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700096)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700096, N'101202', N'申通浏河', N'流动资产', N'借', 2, 700003, 1, N'', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700097)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700097, N'101203', N'申通南郊（科教新城）', N'流动资产', N'借', 2, 700003, 1, N'', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700098)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700098, N'101204', N'申通沙溪', N'流动资产', N'借', 2, 700003, 1, N'', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700099)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700099, N'101205', N'申通市场部', N'流动资产', N'借', 2, 700003, 1, N'', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700100)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700100, N'110101', N'股票', N'流动资产', N'借', 2, 700004, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700101)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700101, N'110102', N'债券', N'流动资产', N'借', 2, 700004, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700102)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700102, N'110103', N'基金', N'流动资产', N'借', 2, 700004, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700103)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700103, N'112201', N'月结账款', N'流动资产', N'借', 2, 700006, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700104)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700104, N'112202', N'客户账款', N'流动资产', N'借', 2, 700006, 1, N'customer', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700105)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700105, N'112210', N'其他账款', N'流动资产', N'借', 2, 700006, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700106)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700106, N'112301', N'供应商预付', N'流动资产', N'借', 2, 700007, 1, N'supplier', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700107)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700107, N'112302', N'预付驿站款', N'流动资产', N'借', 2, 700007, 1, N'supplier', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700108)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700108, N'122101', N'借款', N'流动资产', N'借', 2, 700010, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700109)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700109, N'122102', N'押金', N'流动资产', N'借', 2, 700010, 0, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700110)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700110, N'122103', N'李青', N'流动资产', N'借', 2, 700010, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700111)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700111, N'122104', N'保证金', N'流动资产', N'借', 2, 700010, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700112)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700112, N'122105', N'其他项目垫付款', N'流动资产', N'借', 2, 700010, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700113)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700113, N'122106', N'美申集团', N'流动资产', N'借', 2, 700010, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700114)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700114, N'150101', N'房租费', N'非流动资产', N'借', 2, 700021, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700115)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700115, N'150102', N'物业费', N'非流动资产', N'借', 2, 700021, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700116)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700116, N'150103', N'停车费', N'非流动资产', N'借', 2, 700021, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700117)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700117, N'150104', N'信息系统费用', N'非流动资产', N'借', 2, 700021, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700118)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700118, N'150105', N'采暖费', N'非流动资产', N'借', 2, 700021, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700119)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700119, N'150106', N'宽带费', N'非流动资产', N'借', 2, 700021, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700120)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700120, N'200101', N'河北银行', N'流动负债', N'贷', 2, 700045, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700121)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700121, N'200102', N'邮储银行', N'流动负债', N'贷', 2, 700045, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700122)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700122, N'200103', N'申通总部', N'流动负债', N'贷', 2, 700045, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700123)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700123, N'200104', N'工行长安支行', N'流动负债', N'贷', 2, 700045, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700124)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700124, N'200105', N'其他', N'流动负债', N'贷', 2, 700045, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700125)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700125, N'220201', N'总部应付', N'流动负债', N'贷', 2, 700047, 1, N'express_brand', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700126)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700126, N'220202', N'供应商应付', N'流动负债', N'贷', 2, 700047, 1, N'supplier', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700127)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700127, N'220299', N'其他应付', N'流动负债', N'贷', 2, 700047, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700128)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700128, N'220301', N'预购面单预收', N'流动负债', N'贷', 2, 700048, 1, N'customer', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700129)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700129, N'220399', N'其他预收', N'流动负债', N'贷', 2, 700048, 1, N'customer', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700130)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700130, N'221101', N'工资【正式工】', N'流动负债', N'贷', 2, 700049, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700131)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700131, N'221102', N'工资【临时工】', N'流动负债', N'贷', 2, 700049, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700132)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700132, N'221103', N'职工保险', N'流动负债', N'贷', 2, 700049, 0, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700133)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700133, N'221105', N'住房公积金', N'流动负债', N'贷', 2, 700049, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700134)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700134, N'221106', N'工会经费', N'流动负债', N'贷', 2, 700049, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700135)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700135, N'221107', N'职工教育经费', N'流动负债', N'贷', 2, 700049, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700136)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700136, N'221108', N'非货币性福利', N'流动负债', N'贷', 2, 700049, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700137)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700137, N'221109', N'辞退福利', N'流动负债', N'贷', 2, 700049, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700138)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700138, N'221199', N'其他', N'流动负债', N'贷', 2, 700049, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700139)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700139, N'222101', N'应交增值税', N'流动负债', N'贷', 2, 700050, 0, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700140)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700140, N'222102', N'未交增值税', N'流动负债', N'贷', 2, 700050, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700141)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700141, N'222103', N'预交增值税', N'流动负债', N'借', 2, 700050, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700142)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700142, N'222104', N'待抵扣进项税额', N'流动负债', N'借', 2, 700050, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700143)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700143, N'222105', N'待认证进项税额', N'流动负债', N'借', 2, 700050, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700144)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700144, N'222106', N'待转销项税额', N'流动负债', N'贷', 2, 700050, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700145)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700145, N'222107', N'增值税留抵税额', N'流动负债', N'借', 2, 700050, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700146)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700146, N'222108', N'简易计税', N'流动负债', N'贷', 2, 700050, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700147)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700147, N'222109', N'转出金融商品增值税', N'流动负债', N'贷', 2, 700050, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700148)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700148, N'222110', N'代扣代交增值税', N'流动负债', N'贷', 2, 700050, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700149)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700149, N'222111', N'消费税', N'流动负债', N'贷', 2, 700050, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700150)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700150, N'222112', N'城市维护建设税', N'流动负债', N'贷', 2, 700050, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700151)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700151, N'222113', N'教育费附加', N'流动负债', N'贷', 2, 700050, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700152)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700152, N'222114', N'地方教育附加', N'流动负债', N'贷', 2, 700050, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700153)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700153, N'222115', N'土地增值税', N'流动负债', N'贷', 2, 700050, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700154)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700154, N'222116', N'土地使用税', N'流动负债', N'贷', 2, 700050, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700155)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700155, N'222117', N'房产税', N'流动负债', N'贷', 2, 700050, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700156)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700156, N'222118', N'车船税', N'流动负债', N'贷', 2, 700050, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700157)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700157, N'222119', N'资源税', N'流动负债', N'贷', 2, 700050, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700158)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700158, N'222120', N'矿产资源补偿费', N'流动负债', N'贷', 2, 700050, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700159)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700159, N'222121', N'排污费', N'流动负债', N'贷', 2, 700050, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700160)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700160, N'222124', N'企业所得税', N'流动负债', N'贷', 2, 700050, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700161)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700161, N'222125', N'代扣代交所得税', N'流动负债', N'贷', 2, 700050, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700162)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700162, N'222199', N'其他', N'流动负债', N'贷', 2, 700050, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700163)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700163, N'224101', N'代收货款', N'流动负债', N'贷', 2, 700053, 1, N'customer', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700164)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700164, N'224102', N'风险保证金', N'流动负债', N'贷', 2, 700053, 1, N'supplier', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700165)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700165, N'224104', N'三轮车保险赔偿款', N'流动负债', N'贷', 2, 700053, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700166)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700166, N'224106', N'生育津贴', N'流动负债', N'贷', 2, 700053, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700167)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700167, N'224107', N'代理点保证金', N'流动负债', N'贷', 2, 700053, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700168)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700168, N'224110', N'客户待退款', N'流动负债', N'贷', 2, 700053, 1, N'customer', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700169)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700169, N'300101', N'李青', N'所有者权益', N'贷', 2, 700057, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700170)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700170, N'300102', N'敦新建', N'所有者权益', N'贷', 2, 700057, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700171)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700171, N'310101', N'法定盈余公积', N'所有者权益', N'贷', 2, 700059, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700172)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700172, N'310102', N'任意盈余公积', N'所有者权益', N'贷', 2, 700059, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700173)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700173, N'310401', N'提取法定盈余公积', N'所有者权益', N'借', 2, 700061, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700174)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700174, N'310402', N'提取任意盈余公积', N'所有者权益', N'借', 2, 700061, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700175)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700175, N'310403', N'应付利润', N'所有者权益', N'借', 2, 700061, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700176)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700176, N'310404', N'转作资本的利润', N'所有者权益', N'借', 2, 700061, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700177)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700177, N'310405', N'未分配利润', N'所有者权益', N'贷', 2, 700061, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700178)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700178, N'440101', N'合同成本', N'成本', N'借', 2, 700066, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700179)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700179, N'440102', N'间接费用', N'成本', N'借', 2, 700066, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700180)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700180, N'440103', N'合同毛利', N'成本', N'借', 2, 700066, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700181)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700181, N'500101', N'出港收入', N'营业收入', N'贷', 2, 700069, 0, N'outlet,business_direction,express_brand,business_unit,customer,project', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700182)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700182, N'500102', N'派费', N'营业收入', N'贷', 2, 700069, 0, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700183)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700183, N'500103', N'操作', N'营业收入', N'贷', 2, 700069, 0, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700184)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700184, N'500104', N'增值服务', N'营业收入', N'贷', 2, 700069, 0, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700185)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700185, N'500105', N'政策', N'营业收入', N'贷', 2, 700069, 0, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700186)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700186, N'500106', N'考核激励', N'营业收入', N'贷', 2, 700069, 0, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700187)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700187, N'500107', N'客服', N'营业收入', N'贷', 2, 700069, 0, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700188)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700188, N'505101', N'食堂收入', N'营业收入', N'贷', 2, 700070, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700189)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700189, N'505103', N'其他收入', N'营业收入', N'贷', 2, 700070, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700190)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700190, N'530101', N'盘盈收益', N'其他收益', N'贷', 2, 700075, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700191)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700191, N'530102', N'非流动资产处置净收益', N'其他收益', N'贷', 2, 700075, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700192)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700192, N'530103', N'无法支付的应付账款', N'其他收益', N'贷', 2, 700075, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700193)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700193, N'530107', N'政府补助', N'其他收益', N'贷', 2, 700075, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700194)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700194, N'530108', N'违约金收益', N'其他收益', N'贷', 2, 700075, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700195)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700195, N'530109', N'捐赠收益', N'其他收益', N'贷', 2, 700075, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700196)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700196, N'530199', N'其他', N'其他收益', N'贷', 2, 700075, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700197)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700197, N'540101', N'面单', N'营业成本', N'借', 2, 700076, 0, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700198)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700198, N'540102', N'派费', N'营业成本', N'借', 2, 700076, 0, N'outlet,business_direction,express_brand,business_unit,supplier,employee', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700199)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700199, N'540103', N'中转', N'营业成本', N'借', 2, 700076, 0, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700200)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700200, N'540104', N'操作', N'营业成本', N'借', 2, 700076, 0, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700201)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700201, N'540105', N'增值服务', N'营业成本', N'借', 2, 700076, 0, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700202)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700202, N'540106', N'考核罚款', N'营业成本', N'借', 2, 700076, 0, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700203)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700203, N'540107', N'客服', N'营业成本', N'借', 2, 700076, 0, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700204)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700204, N'540108', N'物料', N'营业成本', N'借', 2, 700076, 0, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700205)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700205, N'540109', N'运输', N'营业成本', N'借', 2, 700076, 0, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700206)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700206, N'540201', N'食堂采购成本', N'营业成本', N'借', 2, 700077, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700207)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700207, N'540299', N'其他成本', N'营业成本', N'借', 2, 700077, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700208)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700208, N'540301', N'消费税', N'营业税金及附加', N'借', 2, 700078, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700209)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700209, N'540302', N'城建税', N'营业税金及附加', N'借', 2, 700078, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700210)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700210, N'540303', N'教育费附加', N'营业税金及附加', N'借', 2, 700078, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700211)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700211, N'540304', N'地方教育附加', N'营业税金及附加', N'借', 2, 700078, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700212)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700212, N'540305', N'土地增值税', N'营业税金及附加', N'借', 2, 700078, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700213)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700213, N'540306', N'土地使用税', N'营业税金及附加', N'借', 2, 700078, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700214)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700214, N'540307', N'房产税', N'营业税金及附加', N'借', 2, 700078, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700215)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700215, N'540308', N'车船税', N'营业税金及附加', N'借', 2, 700078, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700216)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700216, N'540309', N'印花税', N'营业税金及附加', N'借', 2, 700078, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700217)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700217, N'540310', N'资源税', N'营业税金及附加', N'借', 2, 700078, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700218)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700218, N'540311', N'矿产资源补偿费', N'营业税金及附加', N'借', 2, 700078, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700219)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700219, N'540312', N'排污费', N'营业税金及附加', N'借', 2, 700078, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700220)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700220, N'540399', N'其他', N'营业税金及附加', N'借', 2, 700078, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700221)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700221, N'560101', N'薪酬', N'期间费用', N'借', 2, 700079, 0, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700222)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700222, N'560102', N'折旧', N'期间费用', N'借', 2, 700079, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700223)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700223, N'560103', N'摊销', N'期间费用', N'借', 2, 700079, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700224)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700224, N'560104', N'租金', N'期间费用', N'借', 2, 700079, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700225)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700225, N'560105', N'水电费', N'期间费用', N'借', 2, 700079, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700226)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700226, N'560106', N'物管费', N'期间费用', N'借', 2, 700079, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700227)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700227, N'560107', N'维修费', N'期间费用', N'借', 2, 700079, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700228)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700228, N'560108', N'办公费', N'期间费用', N'借', 2, 700079, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700229)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700229, N'560109', N'通讯费', N'期间费用', N'借', 2, 700079, 0, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700230)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700230, N'560110', N'交通费', N'期间费用', N'借', 2, 700079, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700231)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700231, N'560111', N'差旅费', N'期间费用', N'借', 2, 700079, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700232)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700232, N'560112', N'会务费', N'期间费用', N'借', 2, 700079, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700233)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700233, N'560113', N'物流费', N'期间费用', N'借', 2, 700079, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700234)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700234, N'560114', N'招待费', N'期间费用', N'借', 2, 700079, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700235)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700235, N'560115', N'宣传费', N'期间费用', N'借', 2, 700079, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700236)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700236, N'560116', N'装修费', N'期间费用', N'借', 2, 700079, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700237)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700237, N'560117', N'监控费', N'期间费用', N'借', 2, 700079, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700238)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700238, N'560118', N'信息系统费用', N'期间费用', N'借', 2, 700079, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700239)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700239, N'560119', N'招聘费', N'期间费用', N'借', 2, 700079, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700240)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700240, N'560120', N'培训费', N'期间费用', N'借', 2, 700079, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700241)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700241, N'560121', N'外包服务费', N'期间费用', N'借', 2, 700079, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700242)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700242, N'560122', N'事故赔偿费', N'期间费用', N'借', 2, 700079, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700243)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700243, N'560123', N'客户返款', N'期间费用', N'借', 2, 700079, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700244)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700244, N'560124', N'业务用设备', N'期间费用', N'借', 2, 700079, 0, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700245)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700245, N'560131', N'快递柜', N'期间费用', N'借', 2, 700079, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700246)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700246, N'560199', N'其他', N'期间费用', N'借', 2, 700079, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700247)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700247, N'560201', N'薪酬', N'期间费用', N'借', 2, 700080, 0, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700248)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700248, N'560202', N'折旧', N'期间费用', N'借', 2, 700080, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700249)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700249, N'560203', N'摊销', N'期间费用', N'借', 2, 700080, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700250)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700250, N'560204', N'租金', N'期间费用', N'借', 2, 700080, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700251)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700251, N'560205', N'水电费', N'期间费用', N'借', 2, 700080, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700252)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700252, N'560206', N'物管费', N'期间费用', N'借', 2, 700080, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700253)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700253, N'560207', N'维修费', N'期间费用', N'借', 2, 700080, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700254)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700254, N'560208', N'办公费', N'期间费用', N'借', 2, 700080, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700255)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700255, N'560209', N'通讯费', N'期间费用', N'借', 2, 700080, 0, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700256)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700256, N'560210', N'交通费', N'期间费用', N'借', 2, 700080, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700257)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700257, N'560211', N'差旅费', N'期间费用', N'借', 2, 700080, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700258)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700258, N'560212', N'会务费', N'期间费用', N'借', 2, 700080, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700259)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700259, N'560213', N'装修费', N'期间费用', N'借', 2, 700080, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700260)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700260, N'560214', N'招待费', N'期间费用', N'借', 2, 700080, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700261)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700261, N'560215', N'宣传费', N'期间费用', N'借', 2, 700080, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700262)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700262, N'560216', N'咨询费', N'期间费用', N'借', 2, 700080, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700263)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700263, N'560217', N'信息系统费用', N'期间费用', N'借', 2, 700080, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700264)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700264, N'560218', N'监控费', N'期间费用', N'借', 2, 700080, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700265)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700265, N'560219', N'招聘费', N'期间费用', N'借', 2, 700080, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700266)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700266, N'560220', N'培训费', N'期间费用', N'借', 2, 700080, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700267)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700267, N'560221', N'开办费', N'期间费用', N'借', 2, 700080, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700268)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700268, N'560222', N'研发费', N'期间费用', N'借', 2, 700080, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700269)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700269, N'560223', N'外包服务费', N'期间费用', N'借', 2, 700080, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700270)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700270, N'560299', N'其他', N'期间费用', N'借', 2, 700080, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700271)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700271, N'560301', N'利息', N'期间费用', N'借', 2, 700081, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700272)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700272, N'560302', N'手续费', N'期间费用', N'借', 2, 700081, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700273)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700273, N'560303', N'汇兑损益', N'期间费用', N'借', 2, 700081, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700274)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700274, N'560399', N'其他', N'期间费用', N'借', 2, 700081, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700275)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700275, N'571101', N'盘亏毁损', N'其他损失', N'借', 2, 700083, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700276)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700276, N'571102', N'非流动资产处置净损失', N'其他损失', N'借', 2, 700083, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700277)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700277, N'571103', N'坏账损失', N'其他损失', N'借', 2, 700083, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700278)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700278, N'571104', N'长期债券损失', N'其他损失', N'借', 2, 700083, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700279)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700279, N'571105', N'长期股权损失', N'其他损失', N'借', 2, 700083, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700280)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700280, N'571106', N'不可抗力损失', N'其他损失', N'借', 2, 700083, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700281)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700281, N'571107', N'税收滞纳金', N'其他损失', N'借', 2, 700083, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700282)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700282, N'571108', N'违约金损失', N'其他损失', N'借', 2, 700083, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700283)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700283, N'571109', N'捐赠支出', N'其他损失', N'借', 2, 700083, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700284)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700284, N'571110', N'诉讼赔偿款', N'其他损失', N'借', 2, 700083, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700285)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700285, N'571199', N'其他', N'其他损失', N'借', 2, 700083, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700286)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700286, N'10020901', N'支付宝@163', N'流动资产', N'借', 3, 700093, 1, N'', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700287)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700287, N'10020902', N'支付宝8960', N'流动资产', N'借', 3, 700093, 1, N'', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700288)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700288, N'10020903', N'支付宝-建鑫', N'流动资产', N'借', 3, 700093, 1, N'', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700289)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700289, N'10020904', N'支付宝-美鑫', N'流动资产', N'借', 3, 700093, 1, N'', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700290)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700290, N'12210201', N'房租押金', N'流动资产', N'借', 3, 700109, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700291)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700291, N'12210210', N'其他押金', N'流动资产', N'借', 3, 700109, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700292)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700292, N'22110301', N'社会保险', N'流动负债', N'贷', 3, 700132, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700293)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700293, N'22110302', N'商业保险', N'流动负债', N'贷', 3, 700132, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700294)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700294, N'22210101', N'进项税额', N'流动负债', N'借', 3, 700139, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700295)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700295, N'22210102', N'销项税额抵减', N'流动负债', N'借', 3, 700139, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700296)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700296, N'22210103', N'已交税金', N'流动负债', N'借', 3, 700139, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700297)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700297, N'22210104', N'转出未交增值税', N'流动负债', N'借', 3, 700139, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700298)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700298, N'22210105', N'减免税款', N'流动负债', N'借', 3, 700139, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700299)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700299, N'22210106', N'出口抵内销', N'流动负债', N'借', 3, 700139, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700300)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700300, N'22210107', N'销项税额', N'流动负债', N'贷', 3, 700139, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700301)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700301, N'22210108', N'出口退税', N'流动负债', N'贷', 3, 700139, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700302)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700302, N'22210109', N'进项税额转出', N'流动负债', N'贷', 3, 700139, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700303)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700303, N'22210110', N'转出多交增值税', N'流动负债', N'贷', 3, 700139, 1, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700304)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700304, N'50010101', N'现付收入', N'营业收入', N'贷', 3, 700181, 1, N'outlet,business_direction,express_brand,business_unit,customer,project', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700305)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700305, N'50010102', N'月结收入', N'营业收入', N'贷', 3, 700181, 1, N'outlet,business_direction,express_brand,business_unit,customer,project', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700306)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700306, N'50010103', N'到付收入', N'营业收入', N'贷', 3, 700181, 1, N'outlet,business_direction,express_brand,business_unit,customer,project', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700307)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700307, N'50010104', N'总部平台单', N'营业收入', N'贷', 3, 700181, 1, N'outlet,business_direction,express_brand,business_unit,customer,project', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700308)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700308, N'50010201', N'基础派费', N'营业收入', N'贷', 3, 700182, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700309)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700309, N'50010202', N'补贴派费', N'营业收入', N'贷', 3, 700182, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700310)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700310, N'50010203', N'大货派费', N'营业收入', N'贷', 3, 700182, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700311)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700311, N'50010204', N'周期派费', N'营业收入', N'贷', 3, 700182, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700312)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700312, N'50010205', N'调整派费', N'营业收入', N'贷', 3, 700182, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700313)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700313, N'50010206', N'按需派费', N'营业收入', N'贷', 3, 700182, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700314)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700314, N'50010207', N'扶持派费', N'营业收入', N'贷', 3, 700182, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700315)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700315, N'50010208', N'小件员权益', N'营业收入', N'贷', 3, 700182, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700316)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700316, N'50010209', N'考核派费', N'营业收入', N'贷', 3, 700182, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700317)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700317, N'50010210', N'VIP专享派费', N'营业收入', N'贷', 3, 700182, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700318)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700318, N'50010211', N'政策互惠', N'营业收入', N'贷', 3, 700182, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700319)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700319, N'50010301', N'退件操作费', N'营业收入', N'贷', 3, 700183, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700320)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700320, N'50010302', N'其他操作费', N'营业收入', N'贷', 3, 700183, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700321)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700321, N'50010401', N'网点线路跑车', N'营业收入', N'贷', 3, 700184, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700322)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700322, N'50010501', N'政策返利', N'营业收入', N'贷', 3, 700185, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700323)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700323, N'50010502', N'政策考核', N'营业收入', N'贷', 3, 700185, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700324)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700324, N'50010601', N'质量考核', N'营业收入', N'贷', 3, 700186, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700325)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700325, N'50010602', N'时效考核', N'营业收入', N'贷', 3, 700186, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700326)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700326, N'50010603', N'操作规范考核', N'营业收入', N'贷', 3, 700186, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700327)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700327, N'50010604', N'客服类考核', N'营业收入', N'贷', 3, 700186, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700328)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700328, N'50010605', N'车辆考核', N'营业收入', N'贷', 3, 700186, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700329)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700329, N'50010606', N'网管类考核', N'营业收入', N'贷', 3, 700186, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700330)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700330, N'50010607', N'工单考核', N'营业收入', N'贷', 3, 700186, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700331)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700331, N'50010608', N'签收率考核', N'营业收入', N'贷', 3, 700186, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700332)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700332, N'50010609', N'时效件投诉考核', N'营业收入', N'贷', 3, 700186, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700333)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700333, N'50010610', N'虚假问题件考核', N'营业收入', N'贷', 3, 700186, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700334)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700334, N'50010611', N'末端类考核', N'营业收入', N'贷', 3, 700186, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700335)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700335, N'50010612', N'省区综合考核', N'营业收入', N'贷', 3, 700186, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700336)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700336, N'50010613', N'管控类', N'营业收入', N'贷', 3, 700186, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700337)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700337, N'50010614', N'KPI激励', N'营业收入', N'贷', 3, 700186, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700338)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700338, N'50010615', N'扶持基金', N'营业收入', N'贷', 3, 700186, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700339)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700339, N'50010701', N'客服受款', N'营业收入', N'贷', 3, 700187, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700340)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700340, N'50010702', N'三件私了', N'营业收入', N'贷', 3, 700187, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700341)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700341, N'54010101', N'面单费', N'营业成本', N'借', 3, 700197, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700342)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700342, N'54010201', N'基础派费', N'营业成本', N'借', 3, 700198, 1, N'outlet,business_direction,express_brand,business_unit,supplier,employee', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700343)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700343, N'54010202', N'补贴派费', N'营业成本', N'借', 3, 700198, 1, N'outlet,business_direction,express_brand,business_unit,supplier,employee', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700344)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700344, N'54010203', N'大货派费', N'营业成本', N'借', 3, 700198, 1, N'outlet,business_direction,express_brand,business_unit,supplier,employee', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700345)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700345, N'54010204', N'周期派费', N'营业成本', N'借', 3, 700198, 1, N'outlet,business_direction,express_brand,business_unit,supplier,employee', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700346)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700346, N'54010205', N'调整派费', N'营业成本', N'借', 3, 700198, 1, N'outlet,business_direction,express_brand,business_unit,supplier,employee', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700347)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700347, N'54010206', N'按需派费', N'营业成本', N'借', 3, 700198, 1, N'outlet,business_direction,express_brand,business_unit,supplier,employee', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700348)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700348, N'54010207', N'扶持派费', N'营业成本', N'借', 3, 700198, 1, N'outlet,business_direction,express_brand,business_unit,supplier,employee', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700349)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700349, N'54010208', N'小件员权益', N'营业成本', N'借', 3, 700198, 1, N'outlet,business_direction,express_brand,business_unit,supplier,employee', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700350)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700350, N'54010209', N'考核派费', N'营业成本', N'借', 3, 700198, 1, N'outlet,business_direction,express_brand,business_unit,supplier,employee', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700351)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700351, N'54010210', N'代派费', N'营业成本', N'借', 3, 700198, 1, N'outlet,business_direction,express_brand,business_unit,supplier,employee', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700352)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700352, N'54010211', N'承包区派费', N'营业成本', N'借', 3, 700198, 1, N'outlet,business_direction,express_brand,business_unit,supplier,employee', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700353)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700353, N'54010212', N'驿站费用', N'营业成本', N'借', 3, 700198, 1, N'outlet,business_direction,express_brand,business_unit,supplier,employee', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700354)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700354, N'54010301', N'中转费', N'营业成本', N'借', 3, 700199, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700355)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700355, N'54010302', N'中转加收', N'营业成本', N'借', 3, 700199, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700356)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700356, N'54010303', N'中转考核', N'营业成本', N'借', 3, 700199, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700357)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700357, N'54010304', N'全网出港费', N'营业成本', N'借', 3, 700199, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700358)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700358, N'54010305', N'其他中转费', N'营业成本', N'借', 3, 700199, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700359)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700359, N'54010401', N'中心集包费', N'营业成本', N'借', 3, 700200, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700360)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700360, N'54010402', N'三方集包费', N'营业成本', N'借', 3, 700200, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700361)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700361, N'54010403', N'退件操作费', N'营业成本', N'借', 3, 700200, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700362)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700362, N'54010404', N'其他操作费', N'营业成本', N'借', 3, 700200, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700363)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700363, N'54010405', N'装卸费', N'营业成本', N'借', 3, 700200, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700364)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700364, N'54010501', N'经营支持服务费', N'营业成本', N'借', 3, 700201, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700365)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700365, N'54010502', N'网点赋能', N'营业成本', N'借', 3, 700201, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700366)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700366, N'54010503', N'网格仓服务费', N'营业成本', N'借', 3, 700201, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700367)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700367, N'54010504', N'智橙网服务费', N'营业成本', N'借', 3, 700201, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700368)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700368, N'54010505', N'信息技术服务费', N'营业成本', N'借', 3, 700201, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700369)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700369, N'54010601', N'质量考核', N'营业成本', N'借', 3, 700202, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700370)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700370, N'54010602', N'时效考核', N'营业成本', N'借', 3, 700202, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700371)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700371, N'54010603', N'操作规范考核', N'营业成本', N'借', 3, 700202, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700372)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700372, N'54010604', N'客服类考核', N'营业成本', N'借', 3, 700202, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700373)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700373, N'54010605', N'车辆考核', N'营业成本', N'借', 3, 700202, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700374)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700374, N'54010606', N'网管类考核', N'营业成本', N'借', 3, 700202, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700375)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700375, N'54010607', N'工单考核', N'营业成本', N'借', 3, 700202, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700376)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700376, N'54010608', N'签收率考核', N'营业成本', N'借', 3, 700202, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700377)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700377, N'54010609', N'时效件投诉考核', N'营业成本', N'借', 3, 700202, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700378)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700378, N'54010610', N'虚假问题件考核', N'营业成本', N'借', 3, 700202, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700379)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700379, N'54010611', N'末端类考核', N'营业成本', N'借', 3, 700202, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700380)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700380, N'54010612', N'省区综合考核', N'营业成本', N'借', 3, 700202, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700381)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700381, N'54010613', N'管控类罚款', N'营业成本', N'借', 3, 700202, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700382)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700382, N'54010701', N'客服赔款', N'营业成本', N'借', 3, 700203, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700383)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700383, N'54010702', N'三件私了', N'营业成本', N'借', 3, 700203, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700384)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700384, N'54010801', N'辅料', N'营业成本', N'借', 3, 700204, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700385)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700385, N'54010802', N'环保袋', N'营业成本', N'借', 3, 700204, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700386)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700386, N'54010901', N'燃油费', N'营业成本', N'借', 3, 700205, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700387)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700387, N'54010902', N'过路费', N'营业成本', N'借', 3, 700205, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700388)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700388, N'54010903', N'维修费', N'营业成本', N'借', 3, 700205, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700389)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700389, N'54010904', N'违章罚款', N'营业成本', N'借', 3, 700205, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700390)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700390, N'54010905', N'保险费', N'营业成本', N'借', 3, 700205, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700391)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700391, N'54010906', N'检车费', N'营业成本', N'借', 3, 700205, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700392)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700392, N'54010907', N'进门/停车费', N'营业成本', N'借', 3, 700205, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700393)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700393, N'54010908', N'租车费用', N'营业成本', N'借', 3, 700205, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700394)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700394, N'54010909', N'新能源车电费', N'营业成本', N'借', 3, 700205, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700395)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700395, N'54010910', N'购置税', N'营业成本', N'借', 3, 700205, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700396)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700396, N'54010911', N'汽运线路费', N'营业成本', N'借', 3, 700205, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700397)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700397, N'54010912', N'车辆折旧', N'营业成本', N'借', 3, 700205, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700398)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700398, N'54010913', N'其他费用', N'营业成本', N'借', 3, 700205, 1, N'outlet,business_direction,express_brand,business_unit', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700399)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700399, N'56010101', N'工资【正式工】', N'期间费用', N'借', 3, 700221, 0, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700400)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700400, N'56010102', N'工资【临时工】', N'期间费用', N'借', 3, 700221, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700401)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700401, N'56010103', N'奖金', N'期间费用', N'借', 3, 700221, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700402)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700402, N'56010104', N'福利', N'期间费用', N'借', 3, 700221, 0, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700403)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700403, N'56010901', N'电话费', N'期间费用', N'借', 3, 700229, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700404)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700404, N'56010902', N'宽带费', N'期间费用', N'借', 3, 700229, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700405)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700405, N'56012401', N'蓝牙秤', N'期间费用', N'借', 3, 700244, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700406)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700406, N'56012402', N'巴枪', N'期间费用', N'借', 3, 700244, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700407)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700407, N'56012403', N'热敏打印机', N'期间费用', N'借', 3, 700244, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700408)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700408, N'56020101', N'工资【正式工】', N'期间费用', N'借', 3, 700247, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700409)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700409, N'56020102', N'工资【临时工】', N'期间费用', N'借', 3, 700247, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700410)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700410, N'56020103', N'奖金', N'期间费用', N'借', 3, 700247, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700411)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700411, N'56020104', N'福利', N'期间费用', N'借', 3, 700247, 0, NULL, NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700412)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700412, N'56020901', N'电话费', N'期间费用', N'借', 3, 700255, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700413)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700413, N'56020902', N'宽带费', N'期间费用', N'借', 3, 700255, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700414)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700414, N'5601010101', N'业务员', N'期间费用', N'借', 4, 700399, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700415)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700415, N'5601010102', N'客服', N'期间费用', N'借', 4, 700399, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700416)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700416, N'5601010103', N'操作', N'期间费用', N'借', 4, 700399, 1, N'department,business_direction', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700417)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700417, N'5601010104', N'司机', N'期间费用', N'借', 4, 700399, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700418)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700418, N'5601010105', N'业务管理', N'期间费用', N'借', 4, 700399, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700419)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700419, N'5601010106', N'其他', N'期间费用', N'借', 4, 700399, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700420)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700420, N'5601010401', N'社会保险', N'期间费用', N'借', 4, 700402, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700421)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700421, N'5601010402', N'商业保险', N'期间费用', N'借', 4, 700402, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700422)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700422, N'5601010403', N'工伤补助', N'期间费用', N'借', 4, 700402, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700423)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700423, N'5601010499', N'其他福利', N'期间费用', N'借', 4, 700402, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700424)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700424, N'5602010401', N'社会保险', N'期间费用', N'借', 4, 700411, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700425)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700425, N'5602010402', N'商业保险', N'期间费用', N'借', 4, 700411, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700426)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700426, N'5602010403', N'工伤补助', N'期间费用', N'借', 4, 700411, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID] = 700427)
        INSERT INTO [FIN科目] ([FID], [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID], [F是否末级], [F辅助核算], [F外币], [F计算单位], [F启用状态], [F账套ID], [F创建时间], [F更新时间], [F启用年度], [F启用期间]) VALUES (700427, N'5602010499', N'其他福利', N'期间费用', N'借', 4, 700411, 1, N'department', NULL, NULL, 1, 2, N'2026-06-15 00:00:00.000', N'2026-06-15 00:00:00.000', 0, 0);
        SET IDENTITY_INSERT [FIN科目] OFF;
        ");
    }

    private static void MigrateV8(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        ExecSql(ctx, @"
        IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'FIN阿米巴分摊比例')
        DROP TABLE [FIN阿米巴分摊比例];
        ");

        ExecSql(ctx, @"
        DELETE FROM [SYS功能权限] WHERE [F编码] = N'finance:amoeba:allocation';
        ");
    }

    private static void MigrateV9(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;
        // 存量库：删两账套旧科目后重灌品牌版(含各账套真实1002/1012)。账套1/2 凭证/分录/科目余额/辅助核算余额/凭证模板分录全 0 行。
        // FK 处理：引用 FIN科目 的 5 个 FK 中唯 FK_FIN资产类别_折旧科目ID 有数据——FIN资产类别 6 行 F对应折旧科目ID 指向账套1 旧 1602(累计折旧)。
        // 故须：①先把这些引用置 NULL 解除约束 → ②删旧科目 → ③重建品牌版 → ④重指品牌版账套1 新 1602。本方法在 MigrationRunner 单事务内执行，失败整体回滚。
        // V1(全新库)不重跑，故存量库在此守卫覆盖；幂等：MigrationRunner 按版本号只执行一次。
        ExecSql(ctx, @"UPDATE [FIN资产类别] SET [F对应折旧科目ID] = NULL
            WHERE [F对应折旧科目ID] IN (SELECT [FID] FROM [FIN科目] WHERE [F账套ID] IN (1,2));");
        ExecSql(ctx, @"DELETE FROM [FIN科目] WHERE [F账套ID] IN (1,2);");
        InsertBrandAccounts(ctx);
        ExecSql(ctx, @"UPDATE [FIN资产类别] SET [F对应折旧科目ID] =
                (SELECT [FID] FROM [FIN科目] WHERE [F账套ID] = 1 AND [F编码] = N'1602')
            WHERE [F对应折旧科目ID] IS NULL;");
    }

    /// <summary>
    /// 批次6 阿米巴损益项种子(模板3, 72项) —— 一次性守卫重灌(IF NOT EXISTS FID=600)。
    /// V1(全新库) 与 MigrateV6(存量库) 共用; 只灌一次, 不覆盖运行期对模板3的编辑。
    /// 由 docs/superpowers/specs/2026-06-17-阿米巴损益项种子-build.py 生成, 勿手改。
    /// </summary>
    private static void ReseedAmoebaTemplate3(STOTOPDbContext ctx)
    {
        ExecSql(ctx, @"
        SET IDENTITY_INSERT [FIN阿米巴损益项] ON;

        -- 批次6: 一次性守卫重灌(§7.7) —— FID 600 缺失即清模板3并灌新 72 项
        IF NOT EXISTS (SELECT 1 FROM [FIN阿米巴损益项] WHERE [FID] = 600)
        BEGIN
            -- 仅清种子两段(旧414-495 + 新600-671), 不动用户运行期自定义项(其他FID)
            DELETE FROM [FIN阿米巴损益项] WHERE [F模板ID] = 3 AND ([FID] BETWEEN 414 AND 495 OR [FID] BETWEEN 600 AND 671);

            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (600, 3, N'出港', N'group', NULL, 10, 0, NULL, NULL, NULL, NULL, 0, N'section', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (601, 3, N'出港指标', N'group', NULL, 10, 600, NULL, NULL, NULL, NULL, 0, N'section', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (602, 3, N'发件票量', N'indicator', NULL, 10, 601, NULL, NULL, N'manual', NULL, 1, N'indicator', 0, N'票', N'none', NULL, NULL, NULL, NULL, NULL, N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (603, 3, N'发件重量', N'indicator', NULL, 20, 601, NULL, NULL, N'manual', NULL, 1, N'indicator', 0, N'kg', N'none', NULL, NULL, NULL, NULL, NULL, N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (604, 3, N'均重', N'indicator', NULL, 30, 601, NULL, NULL, N'manual', NULL, 1, N'indicator', 0, N'kg/票', N'none', NULL, NULL, NULL, NULL, NULL, N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (605, 3, N'计价件量', N'indicator', NULL, 40, 601, NULL, NULL, N'manual', NULL, 1, N'indicator', 0, N'票', N'none', NULL, NULL, NULL, NULL, NULL, N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (606, 3, N'揽件员人数', N'indicator', NULL, 50, 601, NULL, NULL, N'manual', NULL, 1, N'indicator', 0, N'人', N'none', NULL, NULL, NULL, NULL, NULL, N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (607, 3, N'揽件效能', N'indicator', NULL, 60, 601, NULL, NULL, N'manual', NULL, 1, N'indicator', 0, N'件/人/日', N'none', NULL, NULL, NULL, NULL, NULL, N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (608, 3, N'出港收入', N'group', NULL, 20, 600, NULL, NULL, NULL, NULL, 0, N'section', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (609, 3, N'客户发件收入', N'group', NULL, 10, 608, NULL, NULL, NULL, NULL, 0, N'section', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (610, 3, N'月结收入', N'data', NULL, 10, 609, N'[{""code"": ""50010102"", ""filters"": [{""auxType"": ""business_direction"", ""codes"": [""OUT""]}]}]', NULL, N'system', N'voucher', 0, N'revenue', 0, N'元', N'auto', NULL, NULL, NULL, N'科目余额', N'直接归段', N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (611, 3, N'现付收入', N'data', NULL, 20, 609, N'[{""code"": ""50010101"", ""filters"": [{""auxType"": ""business_direction"", ""codes"": [""OUT""]}]}]', NULL, N'system', N'voucher', 0, N'revenue', 0, N'元', N'auto', NULL, NULL, NULL, N'科目余额', N'直接归段', N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (612, 3, N'到付收入', N'data', NULL, 30, 609, N'[{""code"": ""50010103"", ""filters"": [{""auxType"": ""business_direction"", ""codes"": [""OUT""]}]}]', NULL, N'system', N'voucher', 0, N'revenue', 0, N'元', N'auto', NULL, NULL, NULL, N'科目余额', N'直接归段', N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (613, 3, N'总部平台单', N'data', NULL, 40, 609, N'[{""code"": ""50010104"", ""filters"": [{""auxType"": ""business_direction"", ""codes"": [""OUT""]}]}]', NULL, N'system', N'voucher', 0, N'revenue', 0, N'元', N'auto', NULL, NULL, NULL, N'科目余额', N'直接归段', N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (614, 3, N'政策返利', N'data', NULL, 20, 608, N'[{""code"": ""500105"", ""filters"": [{""auxType"": ""business_direction"", ""codes"": [""OUT""]}]}]', NULL, N'system', N'voucher', 0, N'revenue', 0, N'元', N'auto', NULL, NULL, NULL, N'科目余额', N'直接归段', N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (615, 3, N'出港成本', N'group', NULL, 30, 600, NULL, NULL, NULL, NULL, 0, N'section', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (616, 3, N'面单&派费', N'group', NULL, 10, 615, NULL, NULL, NULL, NULL, 0, N'section', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (617, 3, N'面单', N'data', NULL, 10, 616, N'[{""code"": ""540101"", ""filters"": [{""auxType"": ""business_direction"", ""codes"": [""OUT""]}]}]', NULL, N'system', N'voucher', 0, N'cost', 0, N'元', N'auto', NULL, NULL, NULL, N'科目余额', N'直接归段', N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (618, 3, N'出港派费', N'data', NULL, 20, 616, N'[{""code"": ""540102"", ""filters"": [{""auxType"": ""business_direction"", ""codes"": [""OUT""]}]}]', NULL, N'system', N'voucher', 0, N'cost', 0, N'元', N'auto', NULL, NULL, NULL, N'科目余额', N'直接归段', N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (619, 3, N'中转费', N'group', NULL, 20, 615, NULL, NULL, NULL, NULL, 0, N'section', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (620, 3, N'中转费', N'data', NULL, 10, 619, N'[{""code"": ""54010301"", ""filters"": [{""auxType"": ""business_direction"", ""codes"": [""OUT""]}]}]', NULL, N'system', N'voucher', 0, N'cost', 0, N'元', N'auto', NULL, NULL, NULL, N'科目余额', N'直接归段', N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (621, 3, N'全网出港费', N'data', NULL, 20, 619, N'[{""code"": ""54010304"", ""filters"": [{""auxType"": ""business_direction"", ""codes"": [""OUT""]}]}]', NULL, N'system', N'voucher', 0, N'cost', 0, N'元', N'auto', NULL, NULL, NULL, N'科目余额', N'直接归段', N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (622, 3, N'其他中转', N'data', NULL, 30, 619, N'[{""code"": ""54010302"", ""filters"": [{""auxType"": ""business_direction"", ""codes"": [""OUT""]}]}, {""code"": ""54010303"", ""filters"": [{""auxType"": ""business_direction"", ""codes"": [""OUT""]}]}, {""code"": ""54010305"", ""filters"": [{""auxType"": ""business_direction"", ""codes"": [""OUT""]}]}]', NULL, N'system', N'voucher', 0, N'cost', 0, N'元', N'auto', NULL, NULL, NULL, N'科目余额', N'直接归段', N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (623, 3, N'操作费', N'group', NULL, 30, 615, NULL, NULL, NULL, NULL, 0, N'section', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (624, 3, N'集包费', N'data', NULL, 10, 623, N'[{""code"": ""54010401"", ""filters"": [{""auxType"": ""business_direction"", ""codes"": [""OUT""]}]}, {""code"": ""54010402"", ""filters"": [{""auxType"": ""business_direction"", ""codes"": [""OUT""]}]}]', NULL, N'system', N'voucher', 0, N'cost', 0, N'元', N'auto', NULL, N'volume', N'send', N'科目余额', N'件量分摊(send);发件量为单票基数', N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (625, 3, N'操作费', N'data', NULL, 20, 623, N'[{""code"": ""54010403"", ""filters"": [{""auxType"": ""business_direction"", ""codes"": [""OUT""]}]}, {""code"": ""54010404"", ""filters"": [{""auxType"": ""business_direction"", ""codes"": [""OUT""]}]}, {""code"": ""54010405"", ""filters"": [{""auxType"": ""business_direction"", ""codes"": [""OUT""]}]}]', NULL, N'system', N'voucher', 0, N'cost', 0, N'元', N'auto', NULL, N'volume', N'send', N'科目余额', N'件量分摊(send);发件量为单票基数', N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (626, 3, N'运输费', N'data', NULL, 40, 615, N'[{""code"": ""540109"", ""filters"": [{""auxType"": ""business_direction"", ""codes"": [""OUT""]}]}]', NULL, N'system', N'voucher', 0, N'cost', 0, N'元', N'auto', NULL, NULL, NULL, N'科目余额', N'直接归段', N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (627, 3, N'物料费', N'data', NULL, 50, 615, N'[{""code"": ""540108"", ""filters"": [{""auxType"": ""business_direction"", ""codes"": [""OUT""]}]}]', NULL, N'system', N'voucher', 0, N'cost', 0, N'元', N'auto', NULL, NULL, NULL, N'科目余额', N'直接归段', N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (628, 3, N'增值服务费', N'data', NULL, 60, 615, N'[{""code"": ""540105"", ""filters"": [{""auxType"": ""business_direction"", ""codes"": [""OUT""]}]}]', NULL, N'system', N'voucher', 0, N'cost', 0, N'元', N'auto', NULL, NULL, NULL, N'科目余额', N'直接归段', N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (629, 3, N'考核罚款', N'data', NULL, 70, 615, N'[{""code"": ""540106"", ""filters"": [{""auxType"": ""business_direction"", ""codes"": [""OUT""]}]}]', NULL, N'system', N'voucher', 0, N'cost', 0, N'元', N'auto', NULL, NULL, NULL, N'科目余额', N'直接归段', N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (630, 3, N'客服理赔', N'data', NULL, 80, 615, N'[{""code"": ""540107"", ""filters"": [{""auxType"": ""business_direction"", ""codes"": [""OUT""]}]}]', NULL, N'system', N'voucher', 0, N'cost', 0, N'元', N'auto', NULL, NULL, NULL, N'科目余额', N'直接归段', N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (631, 3, N'揽件员工资', N'data', NULL, 90, 615, N'[{""code"": ""5601010101"", ""filters"": [{""auxType"": ""department"", ""codes"": [""YWY_OUT""]}]}]', NULL, N'system', N'voucher', 0, N'cost', 0, N'元', N'auto', NULL, NULL, NULL, N'科目余额', N'直接归段', N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (632, 3, N'出港操作工资', N'data', NULL, 100, 615, N'[{""code"": ""5601010103"", ""filters"": [{""auxType"": ""business_direction"", ""codes"": [""OUT""]}]}]', NULL, N'system', N'voucher', 0, N'cost', 0, N'元', N'auto', NULL, N'volume', N'send', N'科目余额', N'件量分摊(send);发件量为单票基数', N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (633, 3, N'出港边际利润', N'formula', N'${出港收入} - ${出港成本}', 40, 600, NULL, NULL, N'formula', NULL, 0, N'profit', 0, N'元', N'none', NULL, NULL, NULL, NULL, N'${出港收入} - ${出港成本}', N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (634, 3, N'进港', N'group', NULL, 20, 0, NULL, NULL, NULL, NULL, 0, N'section', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (635, 3, N'进港指标', N'group', NULL, 10, 634, NULL, NULL, NULL, NULL, 0, N'section', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (636, 3, N'派件票量', N'indicator', NULL, 10, 635, NULL, NULL, N'manual', NULL, 1, N'indicator', 0, N'票', N'none', NULL, NULL, NULL, NULL, NULL, N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (637, 3, N'派件重量', N'indicator', NULL, 20, 635, NULL, NULL, N'manual', NULL, 1, N'indicator', 0, N'kg', N'none', NULL, NULL, NULL, NULL, NULL, N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (638, 3, N'入库率', N'indicator', NULL, 30, 635, NULL, NULL, N'manual', NULL, 1, N'indicator', 0, N'%', N'none', NULL, NULL, NULL, NULL, NULL, N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (639, 3, N'派件员人数', N'indicator', NULL, 40, 635, NULL, NULL, N'manual', NULL, 1, N'indicator', 0, N'人', N'none', NULL, NULL, NULL, NULL, NULL, N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (640, 3, N'派件效能', N'indicator', NULL, 50, 635, NULL, NULL, N'manual', NULL, 1, N'indicator', 0, N'件/人/日', N'none', NULL, NULL, NULL, NULL, NULL, N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (641, 3, N'进港收入', N'group', NULL, 20, 634, NULL, NULL, NULL, NULL, 0, N'section', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (642, 3, N'派费收入', N'group', NULL, 10, 641, NULL, NULL, NULL, NULL, 0, N'section', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (643, 3, N'基础派费', N'data', NULL, 10, 642, N'[{""code"": ""50010201"", ""filters"": [{""auxType"": ""business_direction"", ""codes"": [""IN""]}]}]', NULL, N'system', N'voucher', 0, N'revenue', 0, N'元', N'auto', NULL, NULL, NULL, N'科目余额', N'直接归段', N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (644, 3, N'补贴派费', N'data', NULL, 20, 642, N'[{""code"": ""50010202"", ""filters"": [{""auxType"": ""business_direction"", ""codes"": [""IN""]}]}]', NULL, N'system', N'voucher', 0, N'revenue', 0, N'元', N'auto', NULL, NULL, NULL, N'科目余额', N'直接归段', N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (645, 3, N'考核派费', N'data', NULL, 30, 642, N'[{""code"": ""50010209"", ""filters"": [{""auxType"": ""business_direction"", ""codes"": [""IN""]}]}]', NULL, N'system', N'voucher', 0, N'revenue', 0, N'元', N'auto', NULL, NULL, NULL, N'科目余额', N'直接归段', N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (646, 3, N'其他派费', N'data', NULL, 40, 642, N'[{""code"": ""50010203"", ""filters"": [{""auxType"": ""business_direction"", ""codes"": [""IN""]}]}, {""code"": ""50010204"", ""filters"": [{""auxType"": ""business_direction"", ""codes"": [""IN""]}]}, {""code"": ""50010205"", ""filters"": [{""auxType"": ""business_direction"", ""codes"": [""IN""]}]}, {""code"": ""50010206"", ""filters"": [{""auxType"": ""business_direction"", ""codes"": [""IN""]}]}, {""code"": ""50010207"", ""filters"": [{""auxType"": ""business_direction"", ""codes"": [""IN""]}]}, {""code"": ""50010208"", ""filters"": [{""auxType"": ""business_direction"", ""codes"": [""IN""]}]}, {""code"": ""50010210"", ""filters"": [{""auxType"": ""business_direction"", ""codes"": [""IN""]}]}, {""code"": ""50010211"", ""filters"": [{""auxType"": ""business_direction"", ""codes"": [""IN""]}]}]', NULL, N'system', N'voucher', 0, N'revenue', 0, N'元', N'auto', NULL, NULL, NULL, N'科目余额', N'直接归段', N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (647, 3, N'考核激励', N'data', NULL, 20, 641, N'[{""code"": ""500106"", ""filters"": [{""auxType"": ""business_direction"", ""codes"": [""IN""]}]}]', NULL, N'system', N'voucher', 0, N'revenue', 0, N'元', N'auto', NULL, NULL, NULL, N'科目余额', N'直接归段', N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (648, 3, N'客服受款', N'data', NULL, 30, 641, N'[{""code"": ""500107"", ""filters"": [{""auxType"": ""business_direction"", ""codes"": [""IN""]}]}]', NULL, N'system', N'voucher', 0, N'revenue', 0, N'元', N'auto', NULL, NULL, NULL, N'科目余额', N'直接归段', N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (649, 3, N'其他收入(进港操作收入)', N'data', NULL, 40, 641, N'[{""code"": ""500103"", ""filters"": [{""auxType"": ""business_direction"", ""codes"": [""IN""]}]}]', NULL, N'system', N'voucher', 0, N'revenue', 0, N'元', N'auto', NULL, NULL, NULL, N'科目余额', N'直接归段', N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (650, 3, N'进港成本', N'group', NULL, 30, 634, NULL, NULL, NULL, NULL, 0, N'section', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (651, 3, N'操作成本', N'data', NULL, 10, 650, N'[{""code"": ""540104"", ""filters"": [{""auxType"": ""business_direction"", ""codes"": [""IN""]}]}]', NULL, N'system', N'voucher', 0, N'cost', 0, N'元', N'auto', NULL, NULL, NULL, N'科目余额', N'直接归段', N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (652, 3, N'派件成本', N'group', NULL, 20, 650, NULL, NULL, NULL, NULL, 0, N'section', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (653, 3, N'承包区派费', N'data', NULL, 10, 652, N'[{""code"": ""54010211"", ""filters"": [{""auxType"": ""business_direction"", ""codes"": [""IN""]}]}]', NULL, N'system', N'voucher', 0, N'cost', 0, N'元', N'auto', NULL, NULL, NULL, N'科目余额', N'直接归段', N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (654, 3, N'驿站费用', N'data', NULL, 20, 652, N'[{""code"": ""54010212"", ""filters"": [{""auxType"": ""business_direction"", ""codes"": [""IN""]}]}]', NULL, N'system', N'voucher', 0, N'cost', 0, N'元', N'auto', NULL, NULL, NULL, N'科目余额', N'直接归段', N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (655, 3, N'运输成本', N'data', NULL, 30, 650, N'[{""code"": ""540109"", ""filters"": [{""auxType"": ""business_direction"", ""codes"": [""IN""]}]}]', NULL, N'system', N'voucher', 0, N'cost', 0, N'元', N'auto', NULL, NULL, NULL, N'科目余额', N'直接归段', N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (656, 3, N'考核成本', N'data', NULL, 40, 650, N'[{""code"": ""540106"", ""filters"": [{""auxType"": ""business_direction"", ""codes"": [""IN""]}]}]', NULL, N'system', N'voucher', 0, N'cost', 0, N'元', N'auto', NULL, NULL, NULL, N'科目余额', N'直接归段', N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (657, 3, N'赔付成本', N'data', NULL, 50, 650, N'[{""code"": ""540107"", ""filters"": [{""auxType"": ""business_direction"", ""codes"": [""IN""]}]}]', NULL, N'system', N'voucher', 0, N'cost', 0, N'元', N'auto', NULL, NULL, NULL, N'科目余额', N'直接归段', N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (658, 3, N'物料成本', N'data', NULL, 60, 650, N'[{""code"": ""540108"", ""filters"": [{""auxType"": ""business_direction"", ""codes"": [""IN""]}]}]', NULL, N'system', N'voucher', 0, N'cost', 0, N'元', N'auto', NULL, NULL, NULL, N'科目余额', N'直接归段', N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (659, 3, N'客服成本', N'data', NULL, 70, 650, N'[{""code"": ""5601010102"", ""filters"": [{""auxType"": ""department"", ""codes"": [""KF""]}]}, {""code"": ""560121"", ""filters"": [{""auxType"": ""department"", ""codes"": [""KF""]}]}]', NULL, N'system', N'voucher', 0, N'cost', 0, N'元', N'auto', NULL, NULL, NULL, N'科目余额', N'直接归段', N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (660, 3, N'派件员工资', N'data', NULL, 80, 650, N'[{""code"": ""5601010101"", ""filters"": [{""auxType"": ""department"", ""codes"": [""YWY_IN""]}]}]', NULL, N'system', N'voucher', 0, N'cost', 0, N'元', N'auto', NULL, NULL, NULL, N'科目余额', N'直接归段', N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (661, 3, N'进港操作工资', N'data', NULL, 90, 650, N'[{""code"": ""5601010103"", ""filters"": [{""auxType"": ""business_direction"", ""codes"": [""IN""]}]}]', NULL, N'system', N'voucher', 0, N'cost', 0, N'元', N'auto', NULL, N'volume', N'deliver', N'科目余额', N'件量分摊(deliver);派件量为单票基数', N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (662, 3, N'进港边际利润', N'formula', N'${进港收入} - ${进港成本}', 40, 634, NULL, NULL, N'formula', NULL, 0, N'profit', 0, N'元', N'none', NULL, NULL, NULL, NULL, N'${进港收入} - ${进港成本}', N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (663, 3, N'公共分摊', N'group', NULL, 30, 0, NULL, NULL, NULL, NULL, 0, N'section', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (664, 3, N'房租/水电/折旧/摊销', N'group', NULL, 10, 663, NULL, NULL, NULL, NULL, 0, N'section', 0, NULL, NULL, NULL, NULL, NULL, NULL, NULL, N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (665, 3, N'房租', N'data', NULL, 10, 664, N'[{""code"": ""560104""}, {""code"": ""560204""}]', NULL, N'system', N'voucher', 0, N'cost', 0, N'元', N'auto', NULL, N'volume', N'total', N'科目余额', N'件量分摊(total);为单票基数', N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (666, 3, N'水电', N'data', NULL, 20, 664, N'[{""code"": ""560105""}, {""code"": ""560205""}]', NULL, N'system', N'voucher', 0, N'cost', 0, N'元', N'auto', NULL, N'volume', N'total', N'科目余额', N'件量分摊(total);为单票基数', N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (667, 3, N'折旧', N'data', NULL, 30, 664, N'[{""code"": ""560102""}, {""code"": ""560202""}]', NULL, N'system', N'voucher', 0, N'cost', 0, N'元', N'auto', NULL, N'volume', N'total', N'科目余额', N'件量分摊(total);为单票基数', N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (668, 3, N'摊销', N'data', NULL, 40, 664, N'[{""code"": ""560103""}, {""code"": ""560203""}]', NULL, N'system', N'voucher', 0, N'cost', 0, N'元', N'auto', NULL, N'volume', N'total', N'科目余额', N'件量分摊(total);为单票基数', N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (669, 3, N'管理费用', N'data', NULL, 20, 663, N'[{""code"": ""560206""}, {""code"": ""560207""}, {""code"": ""560208""}, {""code"": ""56020901""}, {""code"": ""56020902""}, {""code"": ""560210""}, {""code"": ""560211""}, {""code"": ""560212""}, {""code"": ""560213""}, {""code"": ""560214""}, {""code"": ""560215""}, {""code"": ""560216""}, {""code"": ""560217""}, {""code"": ""560218""}, {""code"": ""560219""}, {""code"": ""560220""}, {""code"": ""560221""}, {""code"": ""560222""}, {""code"": ""560223""}, {""code"": ""560299""}, {""code"": ""5601010105"", ""filters"": [{""auxType"": ""department"", ""codes"": [""YWGL""]}]}]', NULL, N'system', N'voucher', 0, N'cost', 0, N'元', N'auto', NULL, N'volume', N'total', N'科目余额', N'件量分摊(total);为单票基数', N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (670, 3, N'财务费用', N'data', NULL, 30, 663, N'[{""code"": ""5603""}]', NULL, N'system', N'voucher', 0, N'cost', 0, N'元', N'auto', NULL, N'volume', N'total', N'科目余额', N'件量分摊(total);为单票基数', N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
            INSERT INTO [FIN阿米巴损益项] ([FID], [F模板ID], [F项目名称], [F节点角色], [F计算公式], [F排序], [F父ID], [F关联科目JSON], [F计费过滤Json], [F值来源], [F系统数据源], [F是否手工填报], [F项目类别], [F是否指标分区], [F单位], [F单票均模式], [F小数位数], [F分摊方式], [F分摊基数], [F数据来源说明], [F计算逻辑], [F创建时间], [F更新时间]) VALUES (671, 3, N'经营净利润', N'formula', N'${出港边际利润} + ${进港边际利润} - ${公共分摊}', 40, 0, NULL, NULL, N'formula', NULL, 0, N'profit', 0, N'元', N'none', NULL, NULL, NULL, NULL, N'${出港边际利润} + ${进港边际利润} - ${公共分摊}', N'2026-06-17 00:00:00.000', N'2026-06-17 00:00:00.000');
        END

        SET IDENTITY_INSERT [FIN阿米巴损益项] OFF;
        ");
    }

    private static void MigrateV10(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // ① 220301 改名（不动 FID）
        ExecSql(ctx, @"UPDATE [FIN科目] SET [F名称]=N'预收面单'
            WHERE [F编码]=N'220301' AND [F账套ID] IN (1,2);");

        // ② 54010501 改名（不动 FID）
        ExecSql(ctx, @"UPDATE [FIN科目] SET [F名称]=N'总部服务费'
            WHERE [F编码]=N'54010501' AND [F账套ID] IN (1,2);");

        // ③ 删 54010502 网点赋能：先守卫引用为0，再解资产类别折旧引用，再删
        ExecSql(ctx, @"
            IF EXISTS (
                SELECT 1 FROM [FIN凭证分录] e JOIN [FIN科目] k ON e.[F科目ID]=k.[FID]
                WHERE k.[F编码]=N'54010502' AND k.[F账套ID] IN (1,2)
              UNION ALL
                SELECT 1 FROM [FIN科目余额] b JOIN [FIN科目] k ON b.[F科目ID]=k.[FID]
                WHERE k.[F编码]=N'54010502' AND k.[F账套ID] IN (1,2)
              UNION ALL
                SELECT 1 FROM [FIN辅助核算余额] a JOIN [FIN科目] k ON a.[F科目ID]=k.[FID]
                WHERE k.[F编码]=N'54010502' AND k.[F账套ID] IN (1,2)
              UNION ALL
                SELECT 1 FROM [FIN凭证模板分录] t JOIN [FIN科目] k ON t.[F科目ID]=k.[FID]
                WHERE k.[F编码]=N'54010502' AND k.[F账套ID] IN (1,2)
            )
            BEGIN RAISERROR(N'54010502 仍被凭证/余额/模板引用，中止删除', 16, 1); RETURN; END");
        ExecSql(ctx, @"UPDATE [FIN资产类别] SET [F对应折旧科目ID]=NULL
            WHERE [F对应折旧科目ID] IN (SELECT [FID] FROM [FIN科目]
                WHERE [F编码]=N'54010502' AND [F账套ID] IN (1,2));");
        ExecSql(ctx, @"DELETE FROM [FIN科目]
            WHERE [F编码]=N'54010502' AND [F账套ID] IN (1,2);");

        // ④ 新增 50010105 其他出港收入（500101 下末级，借鉴 50010104 形态）
        ExecSql(ctx, @"
            SET IDENTITY_INSERT [FIN科目] ON;
            IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID]=600483)
            INSERT INTO [FIN科目] ([FID],[F编码],[F名称],[F类别],[F余额方向],[F级次],[F父ID],[F是否末级],[F辅助核算],[F外币],[F计算单位],[F启用状态],[F账套ID],[F创建时间],[F更新时间],[F启用年度],[F启用期间])
            VALUES (600483, N'50010105', N'其他出港收入', N'营业收入', N'贷', 3, 600240, 1, N'outlet,business_direction,express_brand,business_unit,customer,project', NULL, NULL, 1, 1, N'2026-06-19 00:00:00.000', N'2026-06-19 00:00:00.000', 0, 0);
            IF NOT EXISTS (SELECT 1 FROM [FIN科目] WHERE [FID]=700428)
            INSERT INTO [FIN科目] ([FID],[F编码],[F名称],[F类别],[F余额方向],[F级次],[F父ID],[F是否末级],[F辅助核算],[F外币],[F计算单位],[F启用状态],[F账套ID],[F创建时间],[F更新时间],[F启用年度],[F启用期间])
            VALUES (700428, N'50010105', N'其他出港收入', N'营业收入', N'贷', 3, 700181, 1, N'outlet,business_direction,express_brand,business_unit,customer,project', NULL, NULL, 1, 2, N'2026-06-19 00:00:00.000', N'2026-06-19 00:00:00.000', 0, 0);
            SET IDENTITY_INSERT [FIN科目] OFF;");
    }

    private static void MigrateV11(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // FID3「快递行业标准模板」明细 = 账套2(太仓美申)科目快照。
        // 模板项 FID 复用科目 FID、F父ID 直接复制 → ApplyTemplate 的 idMapping 还原父子。
        // 排除 1002/1012 下账套私有银行账户子科目；二者本身设为叶子。
        ExecSql(ctx, @"DELETE FROM [FIN科目模板_明细] WHERE [F模板ID]=3;");

        ExecSql(ctx, @"
            SET IDENTITY_INSERT [FIN科目模板_明细] ON;
            INSERT INTO [FIN科目模板_明细]
              ([FID],[F模板ID],[F编码],[F名称],[F类别],[F余额方向],[F级次],[F父ID],[F是否末级],[F辅助核算],[F外币],[F计算单位],[F排序])
            SELECT [FID], 3, [F编码], [F名称], [F类别], [F余额方向], [F级次], [F父ID],
                   CASE WHEN [F编码] IN (N'1002', N'1012') THEN 1 ELSE [F是否末级] END,
                   [F辅助核算], [F外币], [F计算单位], 0
            FROM [FIN科目]
            WHERE [F账套ID]=2
              AND NOT ([F编码] LIKE N'1002%' AND LEN([F编码]) > 4)
              AND NOT ([F编码] LIKE N'1012%' AND LEN([F编码]) > 4);
            SET IDENTITY_INSERT [FIN科目模板_明细] OFF;");

        // 模板说明与实际数据源对齐
        ExecSql(ctx, @"UPDATE [FIN科目模板]
            SET [F说明]=N'源自太仓美申账套快照（排除各账套私有银行账户子科目）'
            WHERE [FID]=3;");
    }
}
