using System.Data;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using STOTOP.Infrastructure.Data;

namespace STOTOP.WebAPI.Data.Seeders;

public static class ExpressSeeder
{
    private const string Module = "Express";

    public static void Migrate(STOTOPDbContext ctx)
    {
        var steps = new List<MigrationStep>
        {
            new(1, "快递计费模块种子数据", MigrateV1),
            new(2, "清理残留废弃列 (2026-05-26)", MigrateV2),
            new(3, "一口价成本矩阵JSON迁移 (2026-05-28)", MigrateV3),
            new(4, "快递报价矩阵JSON迁移 (2026-05-29)", MigrateV4),
            new(5, "成本方案矩阵JSON迁移 (2026-05-29)", MigrateV5),
            new(6, "三表F矩阵JSON统一公式规范化 (2026-06-01)", MigrateV6),
            new(7, "成本方案架构重构数据迁移 (2026-06-01)", MigrateV7),
            new(8, "补充迁移EXP成本项目到标准成本项 (2026-06-01)", MigrateV8),
            new(9, "清理废弃一口价成本表 (2026-06-01)", MigrateV9),
            new(10, "城市种子数据 (2026-06-02)", MigrateV10),
            new(11, "县级行政区种子数据 (2026-06-02)", MigrateV11),
            new(12, "补充云南省缺失城市 (2026-06-02)", MigrateV12),
            new(13, "删除EXP快递报价表F失效日期列 (2026-06-03)", MigrateV13),
            new(14, "清理EXP成本方案表废弃列 (2026-06-03)", MigrateV14),
            new(15, "补充云南自治州城市数据 (2026-06-11)", MigrateV15),
            new(16, "调整EXP快递网点上级编号字段 (2026-06-11)", MigrateV16),
            new(17, "修正承包区孤儿组织引用 (2026-06-12)", MigrateV17),
            new(18, "回填城市加收矩阵缺失的省份ID (2026-06-12)", MigrateV18),
        };
        MigrationRunner.RunMigrations(ctx, Module, steps);
    }

    private static void MigrateV1(STOTOPDbContext context)
    {
        if (!SeederHelper.IsSqlServer(context)) return;

        // 快递品牌种子数据（5条，全局共享 F组织ID=0）
        context.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM [dbo].[EXP品牌] WHERE [F编码] = N'JT')
BEGIN
    INSERT INTO [dbo].[EXP品牌] ([F编码], [F名称], [F状态], [F备注], [F创建时间], [F更新时间]) VALUES (N'JT', N'极兔', 1, N'极兔快递', GETDATE(), GETDATE());
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[EXP品牌] WHERE [F编码] = N'ST')
BEGIN
    INSERT INTO [dbo].[EXP品牌] ([F编码], [F名称], [F状态], [F备注], [F创建时间], [F更新时间]) VALUES (N'ST', N'申通', 1, N'申通快递', GETDATE(), GETDATE());
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[EXP品牌] WHERE [F编码] = N'YD')
BEGIN
    INSERT INTO [dbo].[EXP品牌] ([F编码], [F名称], [F状态], [F备注], [F创建时间], [F更新时间]) VALUES (N'YD', N'韵达', 1, N'韵达快递', GETDATE(), GETDATE());
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[EXP品牌] WHERE [F编码] = N'YT')
BEGIN
    INSERT INTO [dbo].[EXP品牌] ([F编码], [F名称], [F状态], [F备注], [F创建时间], [F更新时间]) VALUES (N'YT', N'圆通', 1, N'圆通快递', GETDATE(), GETDATE());
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[EXP品牌] WHERE [F编码] = N'ZT')
BEGIN
    INSERT INTO [dbo].[EXP品牌] ([F编码], [F名称], [F状态], [F备注], [F创建时间], [F更新时间]) VALUES (N'ZT', N'中通', 1, N'中通快递', GETDATE(), GETDATE());
END
");

        // 省份种子数据（34条含港澳台）
        context.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM [dbo].[EXP省份]) BEGIN
    INSERT INTO [dbo].[EXP省份] ([FID], [F编码], [F名称], [F简称], [F大区], [F偏远]) VALUES
    (1, N'11', N'北京市', N'北京', N'二区', 0),
    (2, N'12', N'天津市', N'天津', N'二区', 0),
    (3, N'13', N'河北省', N'河北', N'二区', 0),
    (4, N'14', N'山西省', N'山西', N'三区', 0),
    (5, N'15', N'内蒙古自治区', N'内蒙古', N'四区', 1),
    (6, N'21', N'辽宁省', N'辽宁', N'三区', 0),
    (7, N'22', N'吉林省', N'吉林', N'三区', 0),
    (8, N'23', N'黑龙江省', N'黑龙江', N'三区', 0),
    (9, N'31', N'上海市', N'上海', N'一区', 0),
    (10, N'32', N'江苏省', N'江苏', N'一区', 0),
    (11, N'33', N'浙江省', N'浙江', N'一区', 0),
    (12, N'34', N'安徽省', N'安徽', N'一区', 0),
    (13, N'35', N'福建省', N'福建', N'二区', 0),
    (14, N'36', N'江西省', N'江西', N'二区', 0),
    (15, N'37', N'山东省', N'山东', N'二区', 0),
    (16, N'41', N'河南省', N'河南', N'二区', 0),
    (17, N'42', N'湖北省', N'湖北', N'二区', 0),
    (18, N'43', N'湖南省', N'湖南', N'二区', 0),
    (19, N'44', N'广东省', N'广东', N'二区', 0),
    (20, N'45', N'广西壮族自治区', N'广西', N'三区', 0),
    (21, N'46', N'海南省', N'海南', N'四区', 0),
    (22, N'50', N'重庆市', N'重庆', N'三区', 0),
    (23, N'51', N'四川省', N'四川', N'三区', 0),
    (24, N'52', N'贵州省', N'贵州', N'三区', 0),
    (25, N'53', N'云南省', N'云南', N'三区', 0),
    (26, N'54', N'西藏自治区', N'西藏', N'五区', 1),
    (27, N'61', N'陕西省', N'陕西', N'三区', 0),
    (28, N'62', N'甘肃省', N'甘肃', N'四区', 0),
    (29, N'63', N'青海省', N'青海', N'四区', 1),
    (30, N'64', N'宁夏回族自治区', N'宁夏', N'四区', 1),
    (31, N'65', N'新疆维吾尔自治区', N'新疆', N'五区', 1),
    (32, N'71', N'台湾省', N'台湾', N'港澳台', 0),
    (33, N'81', N'香港特别行政区', N'香港', N'港澳台', 0),
    (34, N'82', N'澳门特别行政区', N'澳门', N'港澳台', 0);
END
");

        // 成本项目种子数据（14种）
        // 含原始9项 + V4新加4项（集包费/加收类） + V8新加1项（一口价成本）
        context.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM [dbo].[EXP成本项目] WHERE [F编码] = N'LABEL_SERVICE')
BEGIN
    INSERT INTO [dbo].[EXP成本项目] ([F编码], [F名称], [F是否返利], [F排序]) VALUES
    (N'LABEL_SERVICE', N'面单服务费', 0, 1),
    (N'OUTBOUND_DISPATCH', N'出港派费', 0, 2),
    (N'TRANSFER', N'中转费', 0, 3),
    (N'NETWORK_OUTBOUND', N'全网出港费', 0, 4),
    (N'HEAVY_WEIGHT', N'大货计重收费', 0, 5),
    (N'HEAVY_HANDLING', N'大货操作费', 0, 6),
    (N'LOADING', N'操作装卸费', 0, 7),
    (N'LABEL_REBATE', N'面单返利', 1, 8),
    (N'OUTBOUND_HANDLING', N'出港操作费', 0, 9);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[EXP成本项目] WHERE [F编码] = N'PACKING')
BEGIN
    INSERT INTO [dbo].[EXP成本项目] ([F编码], [F名称], [F是否返利], [F排序]) VALUES
    (N'PACKING', N'出港集包费', 0, 10);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[EXP成本项目] WHERE [F编码] = N'DISPATCH_CITY_SURCHARGE')
BEGIN
    INSERT INTO [dbo].[EXP成本项目] ([F编码], [F名称], [F是否返利], [F排序]) VALUES
    (N'DISPATCH_CITY_SURCHARGE', N'出港派费城市加收', 0, 11);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[EXP成本项目] WHERE [F编码] = N'ADDON_DISPATCH_PROVINCE')
BEGIN
    INSERT INTO [dbo].[EXP成本项目] ([F编码], [F名称], [F是否返利], [F排序]) VALUES
    (N'ADDON_DISPATCH_PROVINCE', N'出港周期性加收省份派费', 0, 12);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[EXP成本项目] WHERE [F编码] = N'ADDON_DISPATCH_CITY')
BEGIN
    INSERT INTO [dbo].[EXP成本项目] ([F编码], [F名称], [F是否返利], [F排序]) VALUES
    (N'ADDON_DISPATCH_CITY', N'出港周期性加收城市派费', 0, 13);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[EXP成本项目] WHERE [F编码] = N'FIXED_PRICE')
BEGIN
    INSERT INTO [dbo].[EXP成本项目] ([F编码], [F名称], [F是否返利], [F排序]) VALUES
    (N'FIXED_PRICE', N'一口价成本', 0, 8);
END
");
    }

    private static void MigrateV2(STOTOPDbContext context)
    {
        if (!SeederHelper.IsSqlServer(context)) return;

        // [EXP成本项目]: 清除 F组织ID（先删默认约束再删列）
        SeederHelper.DropColumnSafe(context, "EXP成本项目", "F组织ID");

        // [EXP品牌]: 清除 F组织ID（先删默认约束再删列）
        SeederHelper.DropColumnSafe(context, "EXP品牌", "F组织ID");
    }

    private static void MigrateV3(STOTOPDbContext context)
    {
        if (!SeederHelper.IsSqlServer(context)) return;

        // ===== V3: 一口价成本矩阵JSON迁移 =====
        // 使用 SeederHelper.ExecuteRawSql 避免 ExecuteSqlRaw 内部 String.Format
        // 将 JSON 花括号 { } 误解析为格式占位符导致 FormatException
        SeederHelper.ExecuteRawSql(context, @"
-- 前置检查：确保Schema Auto-Sync已创建F矩阵JSON列
IF COL_LENGTH(N'EXP一口价成本', N'F矩阵JSON') IS NULL
    THROW 50001, N'F矩阵JSON列不存在，Schema Auto-Sync未完成', 1;

-- Step 1: 备份旧表（sp_rename保留完整结构：IDENTITY/PK/索引/约束）
-- 幂等：仅旧表存在且备份不存在时执行
IF OBJECT_ID(N'[EXP一口价成本_矩阵明细]', N'U') IS NOT NULL
  AND OBJECT_ID(N'[EXP一口价成本_矩阵明细_BAK]', N'U') IS NULL
BEGIN
    EXEC sp_rename N'EXP一口价成本_矩阵明细', N'EXP一口价成本_矩阵明细_BAK';
END

IF OBJECT_ID(N'[EXP一口价成本_重量段]', N'U') IS NOT NULL
  AND OBJECT_ID(N'[EXP一口价成本_重量段_BAK]', N'U') IS NULL
BEGIN
    EXEC sp_rename N'EXP一口价成本_重量段', N'EXP一口价成本_重量段_BAK';
END

-- Step 2: 数据转换（从备份表读取，拼装为嵌套JSON写入主表）
IF OBJECT_ID(N'[EXP一口价成本_重量段_BAK]', N'U') IS NOT NULL
  AND EXISTS (SELECT 1 FROM [EXP一口价成本] WHERE [F矩阵JSON] IS NULL)
BEGIN
    UPDATE p SET p.[F矩阵JSON] = ISNULL(
        (
            SELECT 
                seg.[F段序号] AS [segmentIndex],
                seg.[F起始重量] AS [weightFrom],
                seg.[F截止重量] AS [weightTo],
                seg.[F计价方式] AS [pricingMethod],
                seg.[F首重] AS [firstWeight],
                seg.[F续重步进] AS [continueWeight],
                seg.[F重量进位方式] AS [roundingMethod],
                seg.[F进位参数] AS [roundingParam],
                (
                    SELECT 
                        c.[F省份ID] AS [provinceId],
                        c.[F基础价格] AS [basePrice],
                        c.[F续重价格] AS [continuePrice],
                        c.[F首重覆盖] AS [firstWeightOverride],
                        c.[F续重步进覆盖] AS [continueStepOverride],
                        c.[F进位方式覆盖] AS [roundingMethodOverride],
                        c.[F进位参数覆盖] AS [roundingParamOverride]
                    FROM [EXP一口价成本_矩阵明细_BAK] c
                    WHERE c.[F方案ID] = p.[FID] AND c.[F重量段ID] = seg.[FID]
                    FOR JSON PATH
                ) AS [cells]
            FROM [EXP一口价成本_重量段_BAK] seg
            WHERE seg.[F方案ID] = p.[FID]
            ORDER BY seg.[F段序号]
            FOR JSON PATH, ROOT('segments')
        ),
        N'{""segments"":[]}'
    )
    FROM [EXP一口价成本] p
    WHERE p.[F矩阵JSON] IS NULL;
END

-- Step 3: 迁移验证
IF EXISTS (SELECT 1 FROM [EXP一口价成本] WHERE [F矩阵JSON] IS NULL)
    THROW 50002, N'迁移不完整：仍有方案的F矩阵JSON为NULL', 1;
");
    }

    private static void MigrateV4(STOTOPDbContext context)
    {
        if (!SeederHelper.IsSqlServer(context)) return;

        // Step 1: sp_rename 备份旧表（幂等）
        SeederHelper.ExecuteRawSql(context, @"
IF OBJECT_ID('[EXP快递报价_矩阵明细]','U') IS NOT NULL
  AND OBJECT_ID('[EXP快递报价_矩阵明细_BAK]','U') IS NULL
BEGIN
    EXEC sp_rename N'EXP快递报价_矩阵明细', N'EXP快递报价_矩阵明细_BAK';
END

IF OBJECT_ID('[EXP重量段定义]','U') IS NOT NULL
  AND OBJECT_ID('[EXP重量段定义_BAK]','U') IS NULL
BEGIN
    EXEC sp_rename N'EXP重量段定义', N'EXP重量段定义_BAK';
END
");

        // Step 1.5: 列存在性兜底 - 旧 Schema 下 BAK 表可能缺失覆盖列
        // 通过 INFORMATION_SCHEMA.COLUMNS 检查，缺失则 ADD NULL 占位列
        // 目的：保证 Step 2 的 SELECT 在 SQL 编译期不报"列名无效"
        // 使用 EXEC() 包裹 ALTER TABLE，避免父批次因 BAK 表暂不存在而解析失败
        SeederHelper.ExecuteRawSql(context, @"
IF OBJECT_ID('[EXP快递报价_矩阵明细_BAK]','U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                   WHERE TABLE_NAME = N'EXP快递报价_矩阵明细_BAK' AND COLUMN_NAME = N'F首重覆盖')
        EXEC(N'ALTER TABLE [EXP快递报价_矩阵明细_BAK] ADD [F首重覆盖] decimal(18,6) NULL');
    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                   WHERE TABLE_NAME = N'EXP快递报价_矩阵明细_BAK' AND COLUMN_NAME = N'F续重步进覆盖')
        EXEC(N'ALTER TABLE [EXP快递报价_矩阵明细_BAK] ADD [F续重步进覆盖] decimal(18,6) NULL');
    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                   WHERE TABLE_NAME = N'EXP快递报价_矩阵明细_BAK' AND COLUMN_NAME = N'F进位方式覆盖')
        EXEC(N'ALTER TABLE [EXP快递报价_矩阵明细_BAK] ADD [F进位方式覆盖] nvarchar(50) NULL');
    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                   WHERE TABLE_NAME = N'EXP快递报价_矩阵明细_BAK' AND COLUMN_NAME = N'F进位参数覆盖')
        EXEC(N'ALTER TABLE [EXP快递报价_矩阵明细_BAK] ADD [F进位参数覆盖] decimal(18,6) NULL');
END
");

        // Step 2: FOR JSON PATH 嵌套转换
        SeederHelper.ExecuteRawSql(context, @"
IF COL_LENGTH(N'EXP快递报价', N'F矩阵JSON') IS NULL
    THROW 50001, N'F矩阵JSON列不存在，请确认EF模型已同步', 1;

IF OBJECT_ID('[EXP重量段定义_BAK]','U') IS NOT NULL
  AND EXISTS (SELECT 1 FROM [EXP快递报价] WHERE [F矩阵JSON] IS NULL)
BEGIN
    UPDATE q SET q.[F矩阵JSON] = ISNULL(
        (
            SELECT 
                seg.[F段序号] AS [segmentIndex],
                seg.[F起始重量] AS [weightFrom],
                seg.[F截止重量] AS [weightTo],
                seg.[F计价方式] AS [pricingMethod],
                seg.[F首重] AS [firstWeight],
                seg.[F续重步进] AS [continueWeight],
                seg.[F重量进位方式] AS [roundingMethod],
                seg.[F进位参数] AS [roundingParam],
                (
                    SELECT 
                        c.[F省份ID] AS [provinceId],
                        c.[F基础价格] AS [basePrice],
                        c.[F续重价格] AS [continuePrice],
                        c.[F首重覆盖] AS [firstWeightOverride],
                        c.[F续重步进覆盖] AS [continueStepOverride],
                        c.[F进位方式覆盖] AS [roundingMethodOverride],
                        c.[F进位参数覆盖] AS [roundingParamOverride]
                    FROM [EXP快递报价_矩阵明细_BAK] c
                    WHERE c.[F报价方案ID] = q.[FID] AND c.[F重量段ID] = seg.[FID]
                    FOR JSON PATH
                ) AS [cells]
            FROM [EXP重量段定义_BAK] seg
            WHERE seg.[F报价方案ID] = q.[FID]
            ORDER BY seg.[F段序号]
            FOR JSON PATH, ROOT('segments')
        ),
        N'{""segments"":[]}'
    )
    FROM [EXP快递报价] q
    WHERE q.[F矩阵JSON] IS NULL;
END

-- Step 3: 完整性验证
IF EXISTS (SELECT 1 FROM [EXP快递报价] WHERE [F矩阵JSON] IS NULL)
    THROW 50002, N'迁移不完整：仍有报价方案的F矩阵JSON为NULL', 1;
");
    }

    private static void MigrateV5(STOTOPDbContext context)
    {
        if (!SeederHelper.IsSqlServer(context)) return;

        // Step 1: sp_rename 备份旧表（幂等）
        SeederHelper.ExecuteRawSql(context, @"
IF OBJECT_ID('[EXP成本方案_明细]','U') IS NOT NULL
  AND OBJECT_ID('[EXP成本方案_明细_BAK]','U') IS NULL
BEGIN
    EXEC sp_rename N'EXP成本方案_明细', N'EXP成本方案_明细_BAK';
END
");

        // Step 1.5: 列存在性兜底 - 旧 Schema 下 BAK 表可能缺失字段（已废弃或重命名）
        // 通过 INFORMATION_SCHEMA.COLUMNS 检查，缺失则 ADD NULL 占位列
        // 目的：保证 Step 2 的 SELECT 在 SQL 编译期不报"列名无效"
        SeederHelper.ExecuteRawSql(context, @"
IF OBJECT_ID('[EXP成本方案_明细_BAK]','U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                   WHERE TABLE_NAME = N'EXP成本方案_明细_BAK' AND COLUMN_NAME = N'F计费方式')
        EXEC(N'ALTER TABLE [EXP成本方案_明细_BAK] ADD [F计费方式] nvarchar(50) NULL');
    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                   WHERE TABLE_NAME = N'EXP成本方案_明细_BAK' AND COLUMN_NAME = N'F城市名称')
        EXEC(N'ALTER TABLE [EXP成本方案_明细_BAK] ADD [F城市名称] nvarchar(100) NULL');
END
");

        // Step 2: FOR JSON PATH 三级嵌套转换
        // ROW_NUMBER() 在 FOR JSON 子查询中不兼容，包装 derived table sub 解决
        SeederHelper.ExecuteRawSql(context, @"
IF COL_LENGTH(N'EXP成本方案', N'F矩阵JSON') IS NULL
    THROW 50001, N'F矩阵JSON列不存在，请确认EF模型已同步', 1;

IF OBJECT_ID('[EXP成本方案_明细_BAK]','U') IS NOT NULL
  AND EXISTS (SELECT 1 FROM [EXP成本方案] WHERE [F矩阵JSON] IS NULL)
BEGIN
    UPDATE p SET p.[F矩阵JSON] = ISNULL(
        (
            SELECT 
                item.[F成本项目ID] AS [costItemId],
                (
                    SELECT sub.[segmentIndex], sub.[weightFrom], sub.[weightTo],
                           sub.[pricingMethod], sub.[firstWeight], sub.[continueWeight],
                           sub.[weightDeduction], sub.[cells]
                    FROM (
                        SELECT 
                            ROW_NUMBER() OVER(ORDER BY seg.[F起始重量]) AS [segmentIndex],
                            seg.[F起始重量] AS [weightFrom],
                            seg.[F截止重量] AS [weightTo],
                            seg.[F计费方式] AS [pricingMethod],
                            seg.[F首重] AS [firstWeight],
                            seg.[F续重步进] AS [continueWeight],
                            seg.[F扣除重量] AS [weightDeduction],
                            (
                                SELECT 
                                    c.[F省份ID] AS [provinceId],
                                    c.[F城市名称] AS [cityName],
                                    c.[F基础价格] AS [basePrice],
                                    c.[F续重价格] AS [continuePrice]
                                FROM [EXP成本方案_明细_BAK] c
                                WHERE c.[F成本方案ID] = p.[FID]
                                  AND c.[F成本项目ID] = item.[F成本项目ID]
                                  AND ISNULL(c.[F起始重量],0) = seg.[F起始重量]
                                  AND ((c.[F截止重量] IS NULL AND seg.[F截止重量] IS NULL) OR c.[F截止重量] = seg.[F截止重量])
                                FOR JSON PATH
                            ) AS [cells]
                        FROM (
                            SELECT DISTINCT 
                                ISNULL(d.[F起始重量],0) AS [F起始重量],
                                d.[F截止重量],
                                d.[F计费方式],
                                d.[F首重], d.[F续重步进], d.[F扣除重量]
                            FROM [EXP成本方案_明细_BAK] d
                            WHERE d.[F成本方案ID] = p.[FID]
                              AND d.[F成本项目ID] = item.[F成本项目ID]
                        ) seg
                    ) sub
                    ORDER BY sub.[segmentIndex]
                    FOR JSON PATH
                ) AS [segments]
            FROM (
                SELECT DISTINCT [F成本项目ID]
                FROM [EXP成本方案_明细_BAK]
                WHERE [F成本方案ID] = p.[FID]
            ) item
            FOR JSON PATH, ROOT('costItems')
        ),
        N'{""costItems"":[]}'
    )
    FROM [EXP成本方案] p
    WHERE p.[F矩阵JSON] IS NULL;
END

-- Step 3: 完整性验证
IF EXISTS (SELECT 1 FROM [EXP成本方案] WHERE [F矩阵JSON] IS NULL)
    THROW 50002, N'迁移不完整：仍有成本方案的F矩阵JSON为NULL', 1;");
    }

    private static void MigrateV6(STOTOPDbContext context)
    {
        if (!SeederHelper.IsSqlServer(context)) return;

        var connection = context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            connection.Open();
        var transaction = context.Database.CurrentTransaction?.GetDbTransaction();

        // 三张表统一规范化 F矩阵JSON
        foreach (var tableName in new[] { "EXP快递报价", "EXP一口价成本", "EXP成本方案" })
        {
            MigrateV6Table(connection, transaction, tableName);
        }
    }

    private static void MigrateV6Table(System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction? transaction, string tableName)
    {
        // 读取所有记录（FID 为 bigint，使用 Int64）
        var records = new List<(long Id, string Json)>();
        using (var readCmd = connection.CreateCommand())
        {
            readCmd.CommandText = $"SELECT [FID], [F矩阵JSON] FROM [{tableName}] WHERE [F矩阵JSON] IS NOT NULL";
            readCmd.CommandTimeout = 120;
            readCmd.Transaction = transaction;
            using var reader = readCmd.ExecuteReader();
            while (reader.Read())
            {
                records.Add((reader.GetInt64(0), reader.GetString(1)));
            }
        }

        foreach (var (id, json) in records)
        {
            if (string.IsNullOrWhiteSpace(json)) continue;

            var node = JsonNode.Parse(json);
            if (node is not JsonObject) continue;

            // 幂等：如果段级已无 pricingMethod 字段，说明已完成迁移
            if (!NeedsV6Migration(node)) continue;

            TransformV6Matrix(node);

            var newJson = node.ToJsonString();

            using var updateCmd = connection.CreateCommand();
            updateCmd.CommandText = $"UPDATE [{tableName}] SET [F矩阵JSON] = @json WHERE [FID] = @id";
            updateCmd.CommandTimeout = 120;
            updateCmd.Transaction = transaction;

            var jsonParam = updateCmd.CreateParameter();
            jsonParam.ParameterName = "@json";
            jsonParam.Value = newJson;
            updateCmd.Parameters.Add(jsonParam);

            var idParam = updateCmd.CreateParameter();
            idParam.ParameterName = "@id";
            idParam.Value = id;
            updateCmd.Parameters.Add(idParam);

            updateCmd.ExecuteNonQuery();
        }
    }

    private static bool NeedsV6Migration(JsonNode node)
    {
        var obj = node.AsObject();

        // PricingMatrix: { "segments": [...] }
        if (obj.TryGetPropertyValue("segments", out var segs) && segs is JsonArray arr)
        {
            foreach (var seg in arr)
            {
                if (seg?.AsObject().ContainsKey("pricingMethod") == true)
                    return true;
            }
            return false;
        }

        // CostPlanMatrix: { "costItems": [ { "segments": [...] } ] }
        if (obj.TryGetPropertyValue("costItems", out var costItems) && costItems is JsonArray items)
        {
            foreach (var item in items)
            {
                if (item?.AsObject().TryGetPropertyValue("segments", out var itemSegs) == true
                    && itemSegs is JsonArray itemArr)
                {
                    foreach (var seg in itemArr)
                    {
                        if (seg?.AsObject().ContainsKey("pricingMethod") == true)
                            return true;
                    }
                }
            }
        }

        return false;
    }

    private static void TransformV6Matrix(JsonNode node)
    {
        var obj = node.AsObject();

        // CostPlanMatrix: { "costItems": [ { "segments": [...] } ] }
        if (obj.TryGetPropertyValue("costItems", out var costItems) && costItems is JsonArray items)
        {
            foreach (var item in items)
            {
                if (item?.AsObject().TryGetPropertyValue("segments", out var segs) == true
                    && segs is JsonArray arr)
                {
                    TransformV6Segments(arr);
                }
            }
        }
        // PricingMatrix: { "segments": [...] }
        else if (obj.TryGetPropertyValue("segments", out var segments) && segments is JsonArray arr2)
        {
            TransformV6Segments(arr2);
        }
    }

    private static void TransformV6Segments(JsonArray segments)
    {
        foreach (var segNode in segments)
        {
            if (segNode == null) continue;
            var seg = segNode.AsObject();

            // 读取段级字段（修改前先缓存）
            int pricingMethod = GetIntValue(seg, "pricingMethod") ?? 0;
            var continueWeight = GetDecimalValue(seg, "continueWeight");
            var weightDeduction = GetDecimalValue(seg, "weightDeduction");
            int roundingMethod = GetIntValue(seg, "roundingMethod") ?? 1;
            var roundingParam = GetDecimalValue(seg, "roundingParam");

            // --- Segment 级别变换 ---

            // 1. 删除 pricingMethod
            seg.Remove("pricingMethod");

            // 2. 删除 firstWeight（段级首重）
            seg.Remove("firstWeight");

            // 5. roundingParam → truncParam + ceilParam
            seg.Remove("roundingParam");
            if (roundingMethod == 4 || roundingMethod == 6)
            {
                if (roundingParam.HasValue)
                    seg["ceilParam"] = roundingParam.Value;
            }
            // 其他 roundingMethod：丢弃旧值，不添加 truncParam/ceilParam

            // 6. 删除 continueWeight、weightDeduction
            seg.Remove("continueWeight");
            seg.Remove("weightDeduction");

            // --- Cell 级别变换 ---
            if (!seg.TryGetPropertyValue("cells", out var cellsNode) || cellsNode is not JsonArray cells)
                continue;

            foreach (var cellNode in cells)
            {
                if (cellNode == null) continue;
                TransformV6Cell(cellNode.AsObject(), pricingMethod, continueWeight,
                    weightDeduction, roundingMethod);
            }
        }
    }

    private static void TransformV6Cell(JsonObject cell, int pricingMethod,
        decimal? continueWeight, decimal? weightDeduction, int segRoundingMethod)
    {
        // 1. continuePrice: null → 0
        cell["continuePrice"] = GetDecimalValue(cell, "continuePrice") ?? 0m;

        // 2. firstWeightOverride → firstWeight
        //    mode=4 特殊: weightDeduction → cell.firstWeight, cell.basePrice = 0
        var firstWeightOverride = GetDecimalValue(cell, "firstWeightOverride");
        cell.Remove("firstWeightOverride");
        if (pricingMethod == 4)
        {
            cell["firstWeight"] = weightDeduction ?? 0m;
            cell["basePrice"] = 0m;
        }
        else
        {
            cell["firstWeight"] = firstWeightOverride ?? 0m;
        }

        // 3. continueStepOverride → continueStep
        //    下沉逻辑: 无 override 时取段级 continueWeight，否则默认 1
        var continueStepOverride = GetDecimalValue(cell, "continueStepOverride");
        cell.Remove("continueStepOverride");
        if (continueStepOverride.HasValue)
        {
            cell["continueStep"] = continueStepOverride.Value;
        }
        else if (continueWeight.HasValue)
        {
            cell["continueStep"] = continueWeight.Value;
        }
        else
        {
            cell["continueStep"] = 1m;
        }

        // 4. roundingParamOverride → truncParamOverride + ceilParamOverride
        //    结合 effective roundingMethod（cell 自身 override 优先，否则取段级）
        var roundingParamOverride = GetDecimalValue(cell, "roundingParamOverride");
        var roundingMethodOverride = GetIntValue(cell, "roundingMethodOverride");
        cell.Remove("roundingParamOverride");
        // roundingMethodOverride 保留不动

        int effectiveRoundingMethod = roundingMethodOverride ?? segRoundingMethod;
        if (effectiveRoundingMethod == 4 || effectiveRoundingMethod == 6)
        {
            if (roundingParamOverride.HasValue)
                cell["ceilParamOverride"] = roundingParamOverride.Value;
        }
        // 其他 roundingMethod：丢弃旧值，不添加
    }

    private static decimal? GetDecimalValue(JsonObject obj, string propertyName)
    {
        if (!obj.TryGetPropertyValue(propertyName, out var node) || node == null)
            return null;
        if (node.GetValueKind() == JsonValueKind.Number)
            return node.GetValue<decimal>();
        return null;
    }

    private static int? GetIntValue(JsonObject obj, string propertyName)
    {
        if (!obj.TryGetPropertyValue(propertyName, out var node) || node == null)
            return null;
        if (node.GetValueKind() == JsonValueKind.Number)
            return node.GetValue<int>();
        return null;
    }

    private static void MigrateV7(STOTOPDbContext context)
    {
        if (!SeederHelper.IsSqlServer(context)) return;

        // ===== V7: 成本方案架构重构数据迁移 =====
        // 旧EXP成本方案 → 成本项+应用网点+时间段
        // 旧EXP一口价成本 → 方案+成本项+应用网点+关联店铺+时间段

        // 步骤1: 迁移旧成本方案 → 成本项 + 应用网点 + 时间段
        SeederHelper.ExecuteRawSql(context, @"
-- 前置检查：确保新表已由Schema Auto-Sync创建
IF OBJECT_ID(N'[EXP成本方案_成本项]', N'U') IS NULL
    THROW 50001, N'EXP成本方案_成本项表不存在，Schema Auto-Sync未完成', 1;
IF OBJECT_ID(N'[EXP成本方案_成本项_应用网点]', N'U') IS NULL
    THROW 50001, N'EXP成本方案_成本项_应用网点表不存在，Schema Auto-Sync未完成', 1;
IF OBJECT_ID(N'[EXP成本方案_成本项_时间段]', N'U') IS NULL
    THROW 50001, N'EXP成本方案_成本项_时间段表不存在，Schema Auto-Sync未完成', 1;

-- 1a: 为每个旧成本方案创建一个标准成本项（幂等：行级NOT EXISTS）
INSERT INTO [EXP成本方案_成本项] (F方案ID, F成本项名称, F成本项类型, F排序号)
SELECT p.FID, p.F方案名称, 1, 0
FROM [EXP成本方案] p
WHERE p.F矩阵JSON IS NOT NULL
  AND NOT EXISTS (
    SELECT 1 FROM [EXP成本方案_成本项] ci
    WHERE ci.F方案ID = p.FID AND ci.F成本项类型 = 1
  );

-- 1b: 为每个新成本项创建应用网点（从旧方案的F网点ID读取）
INSERT INTO [EXP成本方案_成本项_应用网点] (F成本项ID, F网点ID)
SELECT ci.FID, p.[F网点ID]
FROM [EXP成本方案_成本项] ci
INNER JOIN [EXP成本方案] p ON ci.F方案ID = p.FID
WHERE ci.F成本项类型 = 1
  AND p.[F网点ID] IS NOT NULL AND p.[F网点ID] != 0
  AND NOT EXISTS (
    SELECT 1 FROM [EXP成本方案_成本项_应用网点] ao
    WHERE ao.F成本项ID = ci.FID AND ao.F网点ID = p.[F网点ID]
  );

-- 1c: 为每个新成本项创建时间段（矩阵从旧方案搬迁）
INSERT INTO [EXP成本方案_成本项_时间段] (F成本项ID, F生效日期, F矩阵JSON, F创建时间, F更新时间)
SELECT ci.FID, p.[F生效日期], p.[F矩阵JSON], GETDATE(), GETDATE()
FROM [EXP成本方案_成本项] ci
INNER JOIN [EXP成本方案] p ON ci.F方案ID = p.FID
WHERE ci.F成本项类型 = 1
  AND p.[F矩阵JSON] IS NOT NULL
  AND NOT EXISTS (
    SELECT 1 FROM [EXP成本方案_成本项_时间段] tp
    WHERE tp.F成本项ID = ci.FID
  );
");

        // 步骤2: 迁移旧一口价成本 → 方案 + 成本项 + 应用网点 + 关联店铺 + 时间段
        SeederHelper.ExecuteRawSql(context, @"
-- 前置检查
IF OBJECT_ID(N'[EXP成本方案_成本项_关联店铺]', N'U') IS NULL
    THROW 50001, N'EXP成本方案_成本项_关联店铺表不存在，Schema Auto-Sync未完成', 1;

-- 批量幂等保护：如果已存在一口价成本项，则跳过整个步骤2
IF NOT EXISTS (SELECT 1 FROM [EXP成本方案_成本项] WHERE F成本项类型 = 2)
BEGIN
    -- 2a: 为缺少方案的一口价品牌+组织创建方案
    INSERT INTO [EXP成本方案] ([F品牌编码], [F方案名称], [F状态], [F组织ID], [F创建时间], [F更新时间])
    SELECT DISTINCT fpc.[F品牌编码], N'自动创建-' + fpc.[F品牌编码], 0, fpc.[F组织ID], GETDATE(), GETDATE()
    FROM [EXP一口价成本] fpc
    WHERE fpc.[F状态] != 3
      AND NOT EXISTS (
        SELECT 1 FROM [EXP成本方案] cp
        WHERE cp.[F品牌编码] = fpc.[F品牌编码] AND cp.[F组织ID] = fpc.[F组织ID]
      );

    -- 2b: 创建一口价成本项（每个旧一口价成本 → 一个新成本项）
    INSERT INTO [EXP成本方案_成本项] (F方案ID, F成本项名称, F成本项类型, F结算重量环节, F排序号)
    SELECT cp.FID, fpc.[F方案名称], 2, fpc.[F结算重量环节], 100
    FROM [EXP一口价成本] fpc
    INNER JOIN [EXP成本方案] cp ON cp.[F品牌编码] = fpc.[F品牌编码] AND cp.[F组织ID] = fpc.[F组织ID]
    WHERE fpc.[F状态] != 3;

    -- 2c: 一口价应用网点（从F网点编号转为网点组织ID）
    INSERT INTO [EXP成本方案_成本项_应用网点] (F成本项ID, F网点ID)
    SELECT ci.FID, np.[F组织ID]
    FROM [EXP成本方案_成本项] ci
    INNER JOIN [EXP成本方案] cp ON ci.F方案ID = cp.FID
    INNER JOIN [EXP一口价成本] fpc ON fpc.[F方案名称] = ci.F成本项名称
                                  AND fpc.[F品牌编码] = cp.[F品牌编码]
                                  AND fpc.[F组织ID] = cp.[F组织ID]
    INNER JOIN [EXP快递网点] np ON np.[F编号] = fpc.[F网点编号]
    WHERE ci.F成本项类型 = 2
      AND fpc.[F网点编号] IS NOT NULL;

    -- 2d: 一口价关联店铺（如果旧表存在）
    IF OBJECT_ID(N'[EXP一口价成本_关联店铺]', N'U') IS NOT NULL
    BEGIN
        INSERT INTO [EXP成本方案_成本项_关联店铺] (F成本项ID, F店铺名称, F创建时间)
        SELECT ci.FID, s.[F店铺名称], ISNULL(s.[F创建时间], GETDATE())
        FROM [EXP成本方案_成本项] ci
        INNER JOIN [EXP成本方案] cp ON ci.F方案ID = cp.FID
        INNER JOIN [EXP一口价成本] fpc ON fpc.[F方案名称] = ci.F成本项名称
                                      AND fpc.[F品牌编码] = cp.[F品牌编码]
                                      AND fpc.[F组织ID] = cp.[F组织ID]
        INNER JOIN [EXP一口价成本_关联店铺] s ON s.[F方案ID] = fpc.FID
        WHERE ci.F成本项类型 = 2;
    END

    -- 2e: 一口价时间段
    INSERT INTO [EXP成本方案_成本项_时间段] (F成本项ID, F生效日期, F矩阵JSON, F创建时间, F更新时间)
    SELECT ci.FID, ISNULL(fpc.[F生效日期], GETDATE()), fpc.[F矩阵JSON], GETDATE(), GETDATE()
    FROM [EXP成本方案_成本项] ci
    INNER JOIN [EXP成本方案] cp ON ci.F方案ID = cp.FID
    INNER JOIN [EXP一口价成本] fpc ON fpc.[F方案名称] = ci.F成本项名称
                                  AND fpc.[F品牌编码] = cp.[F品牌编码]
                                  AND fpc.[F组织ID] = cp.[F组织ID]
    WHERE ci.F成本项类型 = 2
      AND fpc.[F矩阵JSON] IS NOT NULL;
END
");
    }

    private static void MigrateV8(STOTOPDbContext context)
    {
        if (!SeederHelper.IsSqlServer(context)) return;

        // ===== V8: 补充迁移EXP成本项目到标准成本项 =====
        // 修复V7步骤1a的错误：V7为每个方案只创建了1条标准成本项（名称=方案名称）
        // 正确应该是从EXP成本项目表读取，为每个方案创建N条标准成本项

        SeederHelper.ExecuteRawSql(context, @"
-- 前置检查：如果EXP成本项目表不存在则跳过
IF OBJECT_ID(N'[EXP成本项目]', N'U') IS NOT NULL
BEGIN
    -- 幂等保护：如果已经有正确迁移的标准成本项（名称来自EXP成本项目且数量>1），则跳过
    IF NOT EXISTS (
        SELECT 1 FROM [EXP成本方案_成本项] ci
        INNER JOIN [EXP成本项目] ic ON ic.[F名称] = ci.F成本项名称
        WHERE ci.F成本项类型 = 1
        HAVING COUNT(*) > 1
    )
    BEGIN
        -- Step 1: 删除V7创建的错误标准成本项的关联数据
        -- 1a: 删除时间段
        DELETE tp FROM [EXP成本方案_成本项_时间段] tp
        INNER JOIN [EXP成本方案_成本项] ci ON tp.F成本项ID = ci.FID
        WHERE ci.F成本项类型 = 1;

        -- 1b: 删除应用网点
        DELETE ao FROM [EXP成本方案_成本项_应用网点] ao
        INNER JOIN [EXP成本方案_成本项] ci ON ao.F成本项ID = ci.FID
        WHERE ci.F成本项类型 = 1;

        -- 1c: 删除错误的标准成本项
        DELETE FROM [EXP成本方案_成本项] WHERE F成本项类型 = 1;

        -- Step 2: 从 EXP成本方案_明细_BAK 确定每个方案关联的成本项目
        -- 优先从BAK表精确获取；如果BAK不存在或为空，则CROSS JOIN全量关联
        IF OBJECT_ID(N'[EXP成本方案_明细_BAK]', N'U') IS NOT NULL
           AND EXISTS (SELECT 1 FROM [EXP成本方案_明细_BAK])
        BEGIN
            -- 从BAK表精确获取每个方案的成本项
            INSERT INTO [EXP成本方案_成本项] (F方案ID, F成本项名称, F成本项类型, F排序号)
            SELECT DISTINCT p.FID, ic.[F名称], 1, ic.[F排序]
            FROM [EXP成本方案] p
            INNER JOIN [EXP成本方案_明细_BAK] d ON d.[F成本方案ID] = p.FID
            INNER JOIN [EXP成本项目] ic ON ic.FID = d.[F成本项目ID]
            WHERE p.[F矩阵JSON] IS NOT NULL;
        END
        ELSE
        BEGIN
            -- BAK表不存在或为空时，为每个有矩阵的方案关联所有成本项目
            INSERT INTO [EXP成本方案_成本项] (F方案ID, F成本项名称, F成本项类型, F排序号)
            SELECT p.FID, ic.[F名称], 1, ic.[F排序]
            FROM [EXP成本方案] p
            CROSS JOIN [EXP成本项目] ic
            WHERE p.[F矩阵JSON] IS NOT NULL
              AND ic.[F编码] != 'FIXED_PRICE';  -- 排除一口价成本项目（由类型2处理）
        END

        -- Step 3: 为每个新建的标准成本项创建时间段
        -- 矩阵JSON从原方案复制（矩阵内部包含所有成本项的数据，前端通过costItemId过滤）
        INSERT INTO [EXP成本方案_成本项_时间段] (F成本项ID, F生效日期, F矩阵JSON, F创建时间, F更新时间)
        SELECT ci.FID, ISNULL(p.[F生效日期], '2020-01-01'), p.[F矩阵JSON], GETDATE(), GETDATE()
        FROM [EXP成本方案_成本项] ci
        INNER JOIN [EXP成本方案] p ON ci.F方案ID = p.FID
        WHERE ci.F成本项类型 = 1
          AND p.[F矩阵JSON] IS NOT NULL
          AND NOT EXISTS (
            SELECT 1 FROM [EXP成本方案_成本项_时间段] tp
            WHERE tp.F成本项ID = ci.FID
          );

        -- Step 4: 复制V7步骤1b的网点逻辑（如果旧方案有F网点ID字段）
        -- 标准成本项的应用网点 = 方案级别的网点（如果存在）
        IF COL_LENGTH('[EXP成本方案]', 'F网点ID') IS NOT NULL
        BEGIN
            INSERT INTO [EXP成本方案_成本项_应用网点] (F成本项ID, F网点ID)
            SELECT ci.FID, p.[F网点ID]
            FROM [EXP成本方案_成本项] ci
            INNER JOIN [EXP成本方案] p ON ci.F方案ID = p.FID
            WHERE ci.F成本项类型 = 1
              AND p.[F网点ID] IS NOT NULL AND p.[F网点ID] != 0
              AND NOT EXISTS (
                SELECT 1 FROM [EXP成本方案_成本项_应用网点] ao
                WHERE ao.F成本项ID = ci.FID AND ao.F网点ID = p.[F网点ID]
              );
        END
    END
END
");
    }

    private static void MigrateV9(STOTOPDbContext context)
    {
        if (!SeederHelper.IsSqlServer(context)) return;

        SeederHelper.ExecuteRawSql(context, @"
-- V9: 清理已废弃的一口价成本表（数据已迁移至EXP成本方案_成本项）
IF OBJECT_ID(N'[EXP一口价成本_关联店铺]', N'U') IS NOT NULL
    DROP TABLE [EXP一口价成本_关联店铺];

IF OBJECT_ID(N'[EXP一口价成本]', N'U') IS NOT NULL
    DROP TABLE [EXP一口价成本];
");
    }

    private static void MigrateV10(STOTOPDbContext context)
    {
        if (!SeederHelper.IsSqlServer(context)) return;

        // 城市种子数据：省会城市 + 主要地级市
        // ProvinceId 与 EXP省份表的FID对应
        context.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM [dbo].[EXP城市]) BEGIN
    SET IDENTITY_INSERT [dbo].[EXP城市] ON;
    INSERT INTO [dbo].[EXP城市] ([FID], [F编码], [F名称], [F省份ID], [F省份名]) VALUES
    -- 北京市(1)
    (1, N'110100', N'北京市', 1, N'北京市'),
    -- 天津市(2)
    (2, N'120100', N'天津市', 2, N'天津市'),
    -- 河北省(3)
    (3, N'130100', N'石家庄市', 3, N'河北省'),
    (4, N'130200', N'唐山市', 3, N'河北省'),
    (5, N'130300', N'秦皇岛市', 3, N'河北省'),
    (6, N'130400', N'邯郸市', 3, N'河北省'),
    (7, N'130500', N'邢台市', 3, N'河北省'),
    (8, N'130600', N'保定市', 3, N'河北省'),
    (9, N'130700', N'张家口市', 3, N'河北省'),
    (10, N'130800', N'承德市', 3, N'河北省'),
    (11, N'130900', N'沧州市', 3, N'河北省'),
    (12, N'131000', N'廊坊市', 3, N'河北省'),
    (13, N'131100', N'衡水市', 3, N'河北省'),
    -- 山西省(4)
    (14, N'140100', N'太原市', 4, N'山西省'),
    (15, N'140200', N'大同市', 4, N'山西省'),
    (16, N'140300', N'阳泉市', 4, N'山西省'),
    (17, N'140400', N'长治市', 4, N'山西省'),
    (18, N'140500', N'晋城市', 4, N'山西省'),
    (19, N'140600', N'朔州市', 4, N'山西省'),
    (20, N'140700', N'晋中市', 4, N'山西省'),
    (21, N'140800', N'运城市', 4, N'山西省'),
    (22, N'140900', N'忻州市', 4, N'山西省'),
    (23, N'141000', N'临汾市', 4, N'山西省'),
    (24, N'141100', N'吕梁市', 4, N'山西省'),
    -- 内蒙古自治区(5)
    (25, N'150100', N'呼和浩特市', 5, N'内蒙古自治区'),
    (26, N'150200', N'包头市', 5, N'内蒙古自治区'),
    (27, N'150300', N'乌海市', 5, N'内蒙古自治区'),
    (28, N'150400', N'赤峰市', 5, N'内蒙古自治区'),
    (29, N'150500', N'通辽市', 5, N'内蒙古自治区'),
    (30, N'150600', N'鄂尔多斯市', 5, N'内蒙古自治区'),
    -- 辽宁省(6)
    (31, N'210100', N'沈阳市', 6, N'辽宁省'),
    (32, N'210200', N'大连市', 6, N'辽宁省'),
    (33, N'210300', N'鞍山市', 6, N'辽宁省'),
    (34, N'210400', N'抚顺市', 6, N'辽宁省'),
    (35, N'210500', N'本溪市', 6, N'辽宁省'),
    (36, N'210600', N'丹东市', 6, N'辽宁省'),
    (37, N'210700', N'锦州市', 6, N'辽宁省'),
    (38, N'210800', N'营口市', 6, N'辽宁省'),
    (39, N'210900', N'阜新市', 6, N'辽宁省'),
    (40, N'211000', N'辽阳市', 6, N'辽宁省'),
    (41, N'211100', N'盘锦市', 6, N'辽宁省'),
    (42, N'211200', N'铁岭市', 6, N'辽宁省'),
    (43, N'211300', N'朝阳市', 6, N'辽宁省'),
    (44, N'211400', N'葫芦岛市', 6, N'辽宁省'),
    -- 吉林省(7)
    (45, N'220100', N'长春市', 7, N'吉林省'),
    (46, N'220200', N'吉林市', 7, N'吉林省'),
    (47, N'220300', N'四平市', 7, N'吉林省'),
    (48, N'220400', N'辽源市', 7, N'吉林省'),
    (49, N'220500', N'通化市', 7, N'吉林省'),
    (50, N'220600', N'白山市', 7, N'吉林省'),
    (51, N'220700', N'松原市', 7, N'吉林省'),
    (52, N'220800', N'白城市', 7, N'吉林省'),
    -- 黑龙江省(8)
    (53, N'230100', N'哈尔滨市', 8, N'黑龙江省'),
    (54, N'230200', N'齐齐哈尔市', 8, N'黑龙江省'),
    (55, N'230300', N'鸡西市', 8, N'黑龙江省'),
    (56, N'230400', N'鹤岗市', 8, N'黑龙江省'),
    (57, N'230500', N'双鸭山市', 8, N'黑龙江省'),
    (58, N'230600', N'大庆市', 8, N'黑龙江省'),
    (59, N'230700', N'伊春市', 8, N'黑龙江省'),
    (60, N'230800', N'佳木斯市', 8, N'黑龙江省'),
    (61, N'230900', N'七台河市', 8, N'黑龙江省'),
    (62, N'231000', N'牡丹江市', 8, N'黑龙江省'),
    (63, N'231100', N'黑河市', 8, N'黑龙江省'),
    (64, N'231200', N'绥化市', 8, N'黑龙江省'),
    -- 上海市(9)
    (65, N'310100', N'上海市', 9, N'上海市'),
    -- 江苏省(10)
    (66, N'320100', N'南京市', 10, N'江苏省'),
    (67, N'320200', N'无锡市', 10, N'江苏省'),
    (68, N'320300', N'徐州市', 10, N'江苏省'),
    (69, N'320400', N'常州市', 10, N'江苏省'),
    (70, N'320500', N'苏州市', 10, N'江苏省'),
    (71, N'320600', N'南通市', 10, N'江苏省'),
    (72, N'320700', N'连云港市', 10, N'江苏省'),
    (73, N'320800', N'淮安市', 10, N'江苏省'),
    (74, N'320900', N'盐城市', 10, N'江苏省'),
    (75, N'321000', N'扬州市', 10, N'江苏省'),
    (76, N'321100', N'镇江市', 10, N'江苏省'),
    (77, N'321200', N'泰州市', 10, N'江苏省'),
    (78, N'321300', N'宿迁市', 10, N'江苏省'),
    -- 浙江省(11)
    (79, N'330100', N'杭州市', 11, N'浙江省'),
    (80, N'330200', N'宁波市', 11, N'浙江省'),
    (81, N'330300', N'温州市', 11, N'浙江省'),
    (82, N'330400', N'嘉兴市', 11, N'浙江省'),
    (83, N'330500', N'湖州市', 11, N'浙江省'),
    (84, N'330600', N'绍兴市', 11, N'浙江省'),
    (85, N'330700', N'金华市', 11, N'浙江省'),
    (86, N'330800', N'衢州市', 11, N'浙江省'),
    (87, N'330900', N'舟山市', 11, N'浙江省'),
    (88, N'331000', N'台州市', 11, N'浙江省'),
    (89, N'331100', N'丽水市', 11, N'浙江省'),
    -- 安徽省(12)
    (90, N'340100', N'合肥市', 12, N'安徽省'),
    (91, N'340200', N'芜湖市', 12, N'安徽省'),
    (92, N'340300', N'蚌埠市', 12, N'安徽省'),
    (93, N'340400', N'淮南市', 12, N'安徽省'),
    (94, N'340500', N'马鞍山市', 12, N'安徽省'),
    (95, N'340600', N'淮北市', 12, N'安徽省'),
    (96, N'340700', N'铜陵市', 12, N'安徽省'),
    (97, N'340800', N'安庆市', 12, N'安徽省'),
    (98, N'341000', N'黄山市', 12, N'安徽省'),
    (99, N'341100', N'滁州市', 12, N'安徽省'),
    (100, N'341200', N'阜阳市', 12, N'安徽省'),
    (101, N'341300', N'宿州市', 12, N'安徽省'),
    (102, N'341500', N'六安市', 12, N'安徽省'),
    (103, N'341600', N'亳州市', 12, N'安徽省'),
    (104, N'341700', N'池州市', 12, N'安徽省'),
    (105, N'341800', N'宣城市', 12, N'安徽省'),
    -- 福建省(13)
    (106, N'350100', N'福州市', 13, N'福建省'),
    (107, N'350200', N'厦门市', 13, N'福建省'),
    (108, N'350300', N'莆田市', 13, N'福建省'),
    (109, N'350400', N'三明市', 13, N'福建省'),
    (110, N'350500', N'泉州市', 13, N'福建省'),
    (111, N'350600', N'漳州市', 13, N'福建省'),
    (112, N'350700', N'南平市', 13, N'福建省'),
    (113, N'350800', N'龙岩市', 13, N'福建省'),
    (114, N'350900', N'宁德市', 13, N'福建省'),
    -- 江西省(14)
    (115, N'360100', N'南昌市', 14, N'江西省'),
    (116, N'360200', N'景德镇市', 14, N'江西省'),
    (117, N'360300', N'萍乡市', 14, N'江西省'),
    (118, N'360400', N'九江市', 14, N'江西省'),
    (119, N'360500', N'新余市', 14, N'江西省'),
    (120, N'360600', N'鹰潭市', 14, N'江西省'),
    (121, N'360700', N'赣州市', 14, N'江西省'),
    (122, N'360800', N'吉安市', 14, N'江西省'),
    (123, N'360900', N'宜春市', 14, N'江西省'),
    (124, N'361000', N'抚州市', 14, N'江西省'),
    (125, N'361100', N'上饶市', 14, N'江西省'),
    -- 山东省(15)
    (126, N'370100', N'济南市', 15, N'山东省'),
    (127, N'370200', N'青岛市', 15, N'山东省'),
    (128, N'370300', N'淄博市', 15, N'山东省'),
    (129, N'370400', N'枣庄市', 15, N'山东省'),
    (130, N'370500', N'东营市', 15, N'山东省'),
    (131, N'370600', N'烟台市', 15, N'山东省'),
    (132, N'370700', N'潍坊市', 15, N'山东省'),
    (133, N'370800', N'济宁市', 15, N'山东省'),
    (134, N'370900', N'泰安市', 15, N'山东省'),
    (135, N'371000', N'威海市', 15, N'山东省'),
    (136, N'371100', N'日照市', 15, N'山东省'),
    (137, N'371300', N'临沂市', 15, N'山东省'),
    (138, N'371400', N'德州市', 15, N'山东省'),
    (139, N'371500', N'聊城市', 15, N'山东省'),
    (140, N'371600', N'滨州市', 15, N'山东省'),
    (141, N'371700', N'菏泽市', 15, N'山东省'),
    -- 河南省(16)
    (142, N'410100', N'郑州市', 16, N'河南省'),
    (143, N'410200', N'开封市', 16, N'河南省'),
    (144, N'410300', N'洛阳市', 16, N'河南省'),
    (145, N'410400', N'平顶山市', 16, N'河南省'),
    (146, N'410500', N'安阳市', 16, N'河南省'),
    (147, N'410600', N'鹤壁市', 16, N'河南省'),
    (148, N'410700', N'新乡市', 16, N'河南省'),
    (149, N'410800', N'焦作市', 16, N'河南省'),
    (150, N'410900', N'濮阳市', 16, N'河南省'),
    (151, N'411000', N'许昌市', 16, N'河南省'),
    (152, N'411100', N'漯河市', 16, N'河南省'),
    (153, N'411200', N'三门峡市', 16, N'河南省'),
    (154, N'411300', N'南阳市', 16, N'河南省'),
    (155, N'411400', N'商丘市', 16, N'河南省'),
    (156, N'411500', N'信阳市', 16, N'河南省'),
    (157, N'411600', N'周口市', 16, N'河南省'),
    (158, N'411700', N'驻马店市', 16, N'河南省'),
    -- 湖北省(17)
    (159, N'420100', N'武汉市', 17, N'湖北省'),
    (160, N'420200', N'黄石市', 17, N'湖北省'),
    (161, N'420300', N'十堰市', 17, N'湖北省'),
    (162, N'420500', N'宜昌市', 17, N'湖北省'),
    (163, N'420600', N'襄阳市', 17, N'湖北省'),
    (164, N'420700', N'鄂州市', 17, N'湖北省'),
    (165, N'420800', N'荆门市', 17, N'湖北省'),
    (166, N'420900', N'孝感市', 17, N'湖北省'),
    (167, N'421000', N'荆州市', 17, N'湖北省'),
    (168, N'421100', N'黄冈市', 17, N'湖北省'),
    (169, N'421200', N'咸宁市', 17, N'湖北省'),
    (170, N'421300', N'随州市', 17, N'湖北省'),
    -- 湖南省(18)
    (171, N'430100', N'长沙市', 18, N'湖南省'),
    (172, N'430200', N'株洲市', 18, N'湖南省'),
    (173, N'430300', N'湘潭市', 18, N'湖南省'),
    (174, N'430400', N'衡阳市', 18, N'湖南省'),
    (175, N'430500', N'邵阳市', 18, N'湖南省'),
    (176, N'430600', N'岳阳市', 18, N'湖南省'),
    (177, N'430700', N'常德市', 18, N'湖南省'),
    (178, N'430800', N'张家界市', 18, N'湖南省'),
    (179, N'430900', N'益阳市', 18, N'湖南省'),
    (180, N'431000', N'郴州市', 18, N'湖南省'),
    (181, N'431100', N'永州市', 18, N'湖南省'),
    (182, N'431200', N'怀化市', 18, N'湖南省'),
    (183, N'431300', N'娄底市', 18, N'湖南省'),
    -- 广东省(19)
    (184, N'440100', N'广州市', 19, N'广东省'),
    (185, N'440200', N'韶关市', 19, N'广东省'),
    (186, N'440300', N'深圳市', 19, N'广东省'),
    (187, N'440400', N'珠海市', 19, N'广东省'),
    (188, N'440500', N'汕头市', 19, N'广东省'),
    (189, N'440600', N'佛山市', 19, N'广东省'),
    (190, N'440700', N'江门市', 19, N'广东省'),
    (191, N'440800', N'湛江市', 19, N'广东省'),
    (192, N'440900', N'茂名市', 19, N'广东省'),
    (193, N'441200', N'肇庆市', 19, N'广东省'),
    (194, N'441300', N'惠州市', 19, N'广东省'),
    (195, N'441400', N'梅州市', 19, N'广东省'),
    (196, N'441500', N'汕尾市', 19, N'广东省'),
    (197, N'441600', N'河源市', 19, N'广东省'),
    (198, N'441700', N'阳江市', 19, N'广东省'),
    (199, N'441800', N'清远市', 19, N'广东省'),
    (200, N'441900', N'东莞市', 19, N'广东省'),
    (201, N'442000', N'中山市', 19, N'广东省'),
    -- 广西壮族自治区(20)
    (202, N'450100', N'南宁市', 20, N'广西壮族自治区'),
    (203, N'450200', N'柳州市', 20, N'广西壮族自治区'),
    (204, N'450300', N'桂林市', 20, N'广西壮族自治区'),
    (205, N'450400', N'梧州市', 20, N'广西壮族自治区'),
    (206, N'450500', N'北海市', 20, N'广西壮族自治区'),
    (207, N'450600', N'防城港市', 20, N'广西壮族自治区'),
    (208, N'450700', N'钦州市', 20, N'广西壮族自治区'),
    (209, N'450800', N'贵港市', 20, N'广西壮族自治区'),
    (210, N'450900', N'玉林市', 20, N'广西壮族自治区'),
    -- 海南省(21)
    (211, N'460100', N'海口市', 21, N'海南省'),
    (212, N'460200', N'三亚市', 21, N'海南省'),
    (213, N'460300', N'儋州市', 21, N'海南省'),
    -- 重庆市(22)
    (214, N'500100', N'重庆市', 22, N'重庆市'),
    -- 四川省(23)
    (215, N'510100', N'成都市', 23, N'四川省'),
    (216, N'510300', N'自贡市', 23, N'四川省'),
    (217, N'510400', N'攀枝花市', 23, N'四川省'),
    (218, N'510500', N'泸州市', 23, N'四川省'),
    (219, N'510600', N'德阳市', 23, N'四川省'),
    (220, N'510700', N'绵阳市', 23, N'四川省'),
    (221, N'510800', N'广元市', 23, N'四川省'),
    (222, N'510900', N'遂宁市', 23, N'四川省'),
    (223, N'511000', N'内江市', 23, N'四川省'),
    (224, N'511100', N'乐山市', 23, N'四川省'),
    (225, N'511300', N'南充市', 23, N'四川省'),
    (226, N'511400', N'眉山市', 23, N'四川省'),
    (227, N'511500', N'宜宾市', 23, N'四川省'),
    -- 贵州省(24)
    (228, N'520100', N'贵阳市', 24, N'贵州省'),
    (229, N'520200', N'六盘水市', 24, N'贵州省'),
    (230, N'520300', N'遵义市', 24, N'贵州省'),
    (231, N'520400', N'安顺市', 24, N'贵州省'),
    (232, N'520500', N'毕节市', 24, N'贵州省'),
    (233, N'520600', N'铜仁市', 24, N'贵州省'),
    -- 云南省(25)
    (234, N'530100', N'昆明市', 25, N'云南省'),
    (235, N'530300', N'曲靖市', 25, N'云南省'),
    (236, N'530400', N'玉溪市', 25, N'云南省'),
    (237, N'530500', N'保山市', 25, N'云南省'),
    (238, N'530600', N'昭通市', 25, N'云南省'),
    (239, N'530700', N'丽江市', 25, N'云南省'),
    (240, N'530800', N'普洱市', 25, N'云南省'),
    (241, N'530900', N'临沧市', 25, N'云南省'),
    (501, N'532300', N'楚雄彝族自治州', 25, N'云南省'),
    (502, N'532900', N'大理白族自治州', 25, N'云南省'),
    -- 西藏自治区(26)
    (242, N'540100', N'拉萨市', 26, N'西藏自治区'),
    (243, N'540200', N'日喀则市', 26, N'西藏自治区'),
    (244, N'540300', N'昌都市', 26, N'西藏自治区'),
    (245, N'540400', N'林芝市', 26, N'西藏自治区'),
    -- 陕西省(27)
    (246, N'610100', N'西安市', 27, N'陕西省'),
    (247, N'610200', N'铜川市', 27, N'陕西省'),
    (248, N'610300', N'宝鸡市', 27, N'陕西省'),
    (249, N'610400', N'咸阳市', 27, N'陕西省'),
    (250, N'610500', N'渭南市', 27, N'陕西省'),
    (251, N'610600', N'延安市', 27, N'陕西省'),
    (252, N'610700', N'汉中市', 27, N'陕西省'),
    (253, N'610800', N'榆林市', 27, N'陕西省'),
    (254, N'610900', N'安康市', 27, N'陕西省'),
    (255, N'611000', N'商洛市', 27, N'陕西省'),
    -- 甘肃省(28)
    (256, N'620100', N'兰州市', 28, N'甘肃省'),
    (257, N'620200', N'嘉峪关市', 28, N'甘肃省'),
    (258, N'620300', N'金昌市', 28, N'甘肃省'),
    (259, N'620400', N'白银市', 28, N'甘肃省'),
    (260, N'620500', N'天水市', 28, N'甘肃省'),
    (261, N'620600', N'武威市', 28, N'甘肃省'),
    (262, N'620700', N'张掖市', 28, N'甘肃省'),
    (263, N'620800', N'平凉市', 28, N'甘肃省'),
    (264, N'620900', N'酒泉市', 28, N'甘肃省'),
    (265, N'621000', N'庆阳市', 28, N'甘肃省'),
    (266, N'621100', N'定西市', 28, N'甘肃省'),
    (267, N'621200', N'陇南市', 28, N'甘肃省'),
    -- 青海省(29)
    (268, N'630100', N'西宁市', 29, N'青海省'),
    (269, N'630200', N'海东市', 29, N'青海省'),
    -- 宁夏回族自治区(30)
    (270, N'640100', N'银川市', 30, N'宁夏回族自治区'),
    (271, N'640200', N'石嘴山市', 30, N'宁夏回族自治区'),
    (272, N'640300', N'吴忠市', 30, N'宁夏回族自治区'),
    (273, N'640400', N'固原市', 30, N'宁夏回族自治区'),
    (274, N'640500', N'中卫市', 30, N'宁夏回族自治区'),
    -- 新疆维吾尔自治区(31)
    (275, N'650100', N'乌鲁木齐市', 31, N'新疆维吾尔自治区'),
    (276, N'650200', N'克拉玛依市', 31, N'新疆维吾尔自治区'),
    (277, N'650400', N'吐鲁番市', 31, N'新疆维吾尔自治区'),
    (278, N'650500', N'哈密市', 31, N'新疆维吾尔自治区'),
    -- 台湾省(32)
    (279, N'710100', N'台北市', 32, N'台湾省'),
    (280, N'710200', N'高雄市', 32, N'台湾省'),
    -- 香港特别行政区(33)
    (281, N'810100', N'香港', 33, N'香港特别行政区'),
    -- 澳门特别行政区(34)
    (282, N'820100', N'澳门', 34, N'澳门特别行政区');
    SET IDENTITY_INSERT [dbo].[EXP城市] OFF;
END
");
    }

    private static void MigrateV11(STOTOPDbContext context)
    {
        if (!SeederHelper.IsSqlServer(context)) return;

        // 县级行政区种子数据：县、县级市、自治县、旗、自治旗、特区、林区
        // 数据来源：2023年国家统计局统计用区划代码
        // 使用分批INSERT（每批最多999行，SQL Server限制单次INSERT...VALUES最多1000行）

        var counties = ExpressCountySeedData.Counties;
        const int batchSize = 999;
        const int startId = 283; // V10已有282条地级市记录

        // 幂等保护：如果已有县级数据则跳过
        var connection = context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            connection.Open();

        var existingCount = 0;
        using (var countCmd = connection.CreateCommand())
        {
            countCmd.CommandText = "SELECT COUNT(*) FROM [EXP城市] WHERE [FID] >= " + startId;
            if (context.Database.CurrentTransaction != null)
                countCmd.Transaction = context.Database.CurrentTransaction.GetDbTransaction();
            existingCount = Convert.ToInt32(countCmd.ExecuteScalar());
        }

        if (existingCount > 0) return; // 已有县级数据，跳过

        for (var batch = 0; batch * batchSize < counties.Length; batch++)
        {
            var skip = batch * batchSize;
            var take = Math.Min(batchSize, counties.Length - skip);
            var batchItems = counties.Skip(skip).Take(take).ToArray();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("SET IDENTITY_INSERT [dbo].[EXP城市] ON;");
            sb.AppendLine("INSERT INTO [dbo].[EXP城市] ([FID], [F编码], [F名称], [F省份ID], [F省份名]) VALUES");

            for (var i = 0; i < batchItems.Length; i++)
            {
                var c = batchItems[i];
                var fid = startId + skip + i;
                var comma = i < batchItems.Length - 1 ? "," : ";";
                sb.AppendLine($"({fid}, N'{c.Code}', N'{c.Name}', {c.ProvinceId}, N'{c.ProvinceName}'){comma}");
            }

            sb.AppendLine("SET IDENTITY_INSERT [dbo].[EXP城市] OFF;");
            context.Database.ExecuteSqlRaw(sb.ToString());
        }
    }

private static void MigrateV12(STOTOPDbContext context)
    {
        if (!SeederHelper.IsSqlServer(context)) return;

        // V12: 补充云南省缺失的两个地级市（楚雄彝族自治州、大理白族自治州）
        // 幂等保护：基于 F编码 IF NOT EXISTS，使用自增ID避免主键冲突
        context.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM [dbo].[EXP城市] WHERE [F编码] = N'532300')
    INSERT INTO [dbo].[EXP城市] ([F编码], [F名称], [F省份ID], [F省份名]) VALUES (N'532300', N'楚雄彝族自治州', 25, N'云南省');
IF NOT EXISTS (SELECT 1 FROM [dbo].[EXP城市] WHERE [F编码] = N'532900')
    INSERT INTO [dbo].[EXP城市] ([F编码], [F名称], [F省份ID], [F省份名]) VALUES (N'532900', N'大理白族自治州', 25, N'云南省');
");
    }

    private static void MigrateV13(STOTOPDbContext context)
    {
        if (!SeederHelper.IsSqlServer(context)) return;

        // V13: 删除EXP快递报价表中未使用的F失效日期列
        // 幂等保护：IF COL_LENGTH 检查列是否存在
        context.Database.ExecuteSqlRaw(@"
IF COL_LENGTH('EXP快递报价', 'F失效日期') IS NOT NULL
    ALTER TABLE [EXP快递报价] DROP COLUMN [F失效日期];
");
    }

    private static void MigrateV14(STOTOPDbContext context)
    {
        if (!SeederHelper.IsSqlServer(context)) return;

        // V14: 清理EXP成本方案表的废弃列（已迁移至成本项/时间段子表）
        // 先删除依赖这些列的索引，再删列
        SeederHelper.ExecuteRawSql(context, @"
-- 删除依赖于待删列的所有非聚集索引（排除主键），使用 IF EXISTS 保护幂等
DECLARE @dropSql NVARCHAR(MAX) = N'';
SELECT @dropSql += 'IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N''EXP成本方案'') AND name = N''' + i.name + ''') DROP INDEX [' + i.name + '] ON [EXP成本方案];' + CHAR(13)
FROM sys.indexes i
INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
WHERE i.object_id = OBJECT_ID(N'EXP成本方案')
  AND c.name IN (N'F生效日期', N'F失效日期', N'F版本', N'F网点ID', N'F前一版本ID', N'F矩阵JSON')
  AND i.is_primary_key = 0;
IF @dropSql <> N'' EXEC sp_executesql @dropSql;
");

        // 使用 DropColumnSafe 安全删除列（自动处理默认约束）
        SeederHelper.DropColumnSafe(context, "EXP成本方案", "F生效日期");
        SeederHelper.DropColumnSafe(context, "EXP成本方案", "F失效日期");
        SeederHelper.DropColumnSafe(context, "EXP成本方案", "F版本");
        SeederHelper.DropColumnSafe(context, "EXP成本方案", "F网点ID");
        SeederHelper.DropColumnSafe(context, "EXP成本方案", "F前一版本ID");

        SeederHelper.DropColumnSafe(context, "EXP成本方案", "F矩阵JSON");
    }

    private static void MigrateV15(STOTOPDbContext context)
    {
        if (!SeederHelper.IsSqlServer(context)) return;

        // V15: 补充成本项目城市加收搜索需要的云南自治州城市数据。
        // 基于 F编码 upsert，使用自增ID避免与县级种子数据主键冲突。
        context.Database.ExecuteSqlRaw(@"
IF EXISTS (SELECT 1 FROM [dbo].[EXP城市] WHERE [F编码] = N'532300')
    UPDATE [dbo].[EXP城市] SET [F名称] = N'楚雄彝族自治州', [F省份ID] = 25, [F省份名] = N'云南省' WHERE [F编码] = N'532300';
ELSE
    INSERT INTO [dbo].[EXP城市] ([F编码], [F名称], [F省份ID], [F省份名]) VALUES (N'532300', N'楚雄彝族自治州', 25, N'云南省');

IF EXISTS (SELECT 1 FROM [dbo].[EXP城市] WHERE [F编码] = N'532500')
    UPDATE [dbo].[EXP城市] SET [F名称] = N'红河哈尼族彝族自治州', [F省份ID] = 25, [F省份名] = N'云南省' WHERE [F编码] = N'532500';
ELSE
    INSERT INTO [dbo].[EXP城市] ([F编码], [F名称], [F省份ID], [F省份名]) VALUES (N'532500', N'红河哈尼族彝族自治州', 25, N'云南省');

IF EXISTS (SELECT 1 FROM [dbo].[EXP城市] WHERE [F编码] = N'532900')
    UPDATE [dbo].[EXP城市] SET [F名称] = N'大理白族自治州', [F省份ID] = 25, [F省份名] = N'云南省' WHERE [F编码] = N'532900';
ELSE
    INSERT INTO [dbo].[EXP城市] ([F编码], [F名称], [F省份ID], [F省份名]) VALUES (N'532900', N'大理白族自治州', 25, N'云南省');

IF EXISTS (SELECT 1 FROM [dbo].[EXP城市] WHERE [F编码] = N'533100')
    UPDATE [dbo].[EXP城市] SET [F名称] = N'德宏傣族景颇族自治州', [F省份ID] = 25, [F省份名] = N'云南省' WHERE [F编码] = N'533100';
ELSE
    INSERT INTO [dbo].[EXP城市] ([F编码], [F名称], [F省份ID], [F省份名]) VALUES (N'533100', N'德宏傣族景颇族自治州', 25, N'云南省');
");
    }

    private static void MigrateV16(STOTOPDbContext context)
    {
        if (!SeederHelper.IsSqlServer(context)) return;

        // V16: 删除不再使用的源UID，并将上级网点关系改为直接保存网点编号。
        // 兼容 SchemaAutoSync 已先创建新列的开发库：先复制旧列数据，再清理旧列。
        SeederHelper.ExecuteRawSql(context, @"
IF COL_LENGTH(N'EXP快递网点', N'F上级网点源UID') IS NOT NULL
   AND COL_LENGTH(N'EXP快递网点', N'F上级网点编号') IS NULL
BEGIN
    EXEC sp_rename N'[dbo].[EXP快递网点].[F上级网点源UID]', N'F上级网点编号', N'COLUMN';
END

IF COL_LENGTH(N'EXP快递网点', N'F上级网点源UID') IS NOT NULL
   AND COL_LENGTH(N'EXP快递网点', N'F上级网点编号') IS NOT NULL
BEGIN
    EXEC sp_executesql N'UPDATE [EXP快递网点]
       SET [F上级网点编号] = [F上级网点源UID]
     WHERE NULLIF(LTRIM(RTRIM([F上级网点编号])), N'''') IS NULL
       AND NULLIF(LTRIM(RTRIM([F上级网点源UID])), N'''') IS NOT NULL;';
END

IF COL_LENGTH(N'EXP快递网点', N'F上级网点编号') IS NOT NULL
BEGIN
    ALTER TABLE [EXP快递网点] ALTER COLUMN [F上级网点编号] nvarchar(50) NULL;
END

IF COL_LENGTH(N'EXP快递网点', N'F上级网点编号') IS NULL
BEGIN
    ALTER TABLE [EXP快递网点] ADD [F上级网点编号] nvarchar(50) NULL;
END
");

        SeederHelper.DropColumnSafe(context, "EXP快递网点", "F源UID");
        SeederHelper.DropColumnSafe(context, "EXP快递网点", "F上级网点源UID");
    }

    private static void MigrateV17(STOTOPDbContext context)
    {
        if (!SeederHelper.IsSqlServer(context)) return;

        SeederHelper.ExecuteRawSql(context, @"
IF NOT EXISTS (SELECT 1 FROM [dbo].[SYS组织架构] WHERE [FID] = 192)
BEGIN
    THROW 50017, N'无法修正承包区孤儿组织引用：组织ID=192不存在', 1;
END

UPDATE area
   SET [F组织ID] = 192
  FROM [dbo].[EXP承包区] area
 WHERE NOT EXISTS (
       SELECT 1
         FROM [dbo].[SYS组织架构] org
        WHERE org.[FID] = area.[F组织ID]
   );
");
    }

    private static void MigrateV18(STOTOPDbContext context)
    {
        if (!SeederHelper.IsSqlServer(context)) return;

        // V18: 回填城市加收矩阵单元格缺失的省份ID（按 cityId 查 EXP城市）。
        // 旧版前端保存 city 模式矩阵时不携带 provinceId，而计费引擎 FindCellCity
        // 按 (省份ID, 城市名) 匹配，缺省份的城市行永远不命中 → 城市加收成本被静默漏算。
        // 矩阵模型与计费引擎/读写接口共用 PricingMatrixSerializer，round-trip 不会丢失引擎可见字段。
        var cityProvinceMap = context.Set<STOTOP.Module.Express.Entities.ExpCity>()
            .ToDictionary(c => c.FID, c => c.FProvinceId);
        if (cityProvinceMap.Count == 0) return;

        var periods = context.Set<STOTOP.Module.Express.Entities.ExpCostPlanItemPeriod>()
            .AsTracking()
            .Where(p => p.FMatrixJson != null && p.FMatrixJson.Contains("\"cityId\""))
            .ToList();

        foreach (var period in periods)
        {
            STOTOP.Module.Express.Models.CostPlanMatrix matrix;
            try
            {
                matrix = STOTOP.Module.Express.Models.PricingMatrixSerializer.DeserializeCostPlan(period.FMatrixJson);
            }
            catch (JsonException)
            {
                continue; // 坏 JSON 跳过，不阻塞迁移
            }

            var changed = false;
            foreach (var entry in matrix.CostItems)
            {
                if (!string.Equals(entry.PricingScope, "city", StringComparison.OrdinalIgnoreCase))
                    continue;
                foreach (var cell in entry.Segments.SelectMany(s => s.Cells))
                {
                    if (cell.ProvinceId == 0 && cell.CityId is > 0
                        && cityProvinceMap.TryGetValue(cell.CityId.Value, out var provinceId)
                        && provinceId > 0)
                    {
                        cell.ProvinceId = provinceId;
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                period.FMatrixJson = STOTOP.Module.Express.Models.PricingMatrixSerializer.SerializeCostPlan(matrix);
                period.FUpdatedTime = DateTime.Now;
            }
        }

        context.SaveChanges();
    }
}
