-- =============================================
-- EXP 业务对象引用列 [F业务对象ID]: bigint → nvarchar(50) 迁移脚本
-- 范围: Express 模块 12 张表的 [F业务对象ID] 引用列（不动各表自身主键 [FID] bigint）
-- 背景:
--   [F业务对象ID] + [F业务对象类型] 是一组多态外键，指向 客户(KH)/代理(DL)/网点(WD)/业务员(YW)/承包(CB)/驿站(YZ)。
--   被引用对象以字符串编号为键（如 CRM客户 主键 F编号 = nvarchar(50)，不含数字 FID），故引用列应为 nvarchar(50)。
--   库中遗留为 bigint（早于当前 EF 模型），SchemaAutoSync 对跨类型只"跳过"不自愈，导致列型长期漂移。
-- 日期: 2026-06-28
-- 说明:
--   1. 幂等：每表以"列仍为 bigint"为执行守卫，重复执行自动跳过；改完即 no-op。
--   2. 安全前置：仅当 12 张表全部为空时执行；若有数据则中止（bigint→nvarchar 涉及既有值语义复核，需人工确认）。
--   3. 9 张表的 [F业务对象ID] 上有非聚集索引（其中 2 个为唯一索引），按库中原定义 drop → 改列 → 原样重建。
--   4. 整脚本事务包裹（XACT_ABORT）；列可空性保持与 EF 模型一致：
--      [EXP出港运单_历史] / [EXP出港账单审核规则] 为 NULL，其余 10 张为 NOT NULL。
-- =============================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
BEGIN TRANSACTION;

-- ========== Step 0: 前置检查（仅允许在空表上执行） ==========
PRINT N'[Step 0] 前置检查：确认 12 张目标表均为空...';

DECLARE @nonEmpty nvarchar(2048);
SELECT @nonEmpty = STRING_AGG(x.TableName + N'(' + CAST(x.Rows AS nvarchar(20)) + N')', N', ')
FROM (
    SELECT t.name AS TableName, SUM(p.rows) AS Rows
    FROM sys.tables t
    JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0, 1)
    WHERE t.name IN (N'EXP出港运单_历史', N'EXP出港账单审核规则', N'EXP费用减免', N'EXP均重上限',
                     N'EXP客户返利', N'EXP客户运单号余额', N'EXP目的地占比', N'EXP预付款记录',
                     N'EXP预付款流水', N'EXP预付款余额', N'EXP月度调整', N'EXP运单号交易')
    GROUP BY t.name
    HAVING SUM(p.rows) > 0
) x;

IF @nonEmpty IS NOT NULL
BEGIN
    SET @nonEmpty = N'迁移已中止：以下表非空，bigint→nvarchar(50) 涉及既有数据语义复核，请人工确认后手动处理 → ' + @nonEmpty;
    THROW 51000, @nonEmpty, 1;
END

-- ========== Step 1: 无索引依赖的表（仅改列） ==========

-- ----- [EXP出港运单_历史]（可空） -----
IF EXISTS (SELECT 1 FROM sys.columns c JOIN sys.types ty ON ty.user_type_id = c.user_type_id
           WHERE c.object_id = OBJECT_ID(N'[dbo].[EXP出港运单_历史]') AND c.name = N'F业务对象ID' AND ty.name = N'bigint')
BEGIN
    PRINT N'[EXP出港运单_历史] F业务对象ID: bigint → nvarchar(50) NULL';
    ALTER TABLE [dbo].[EXP出港运单_历史] ALTER COLUMN [F业务对象ID] nvarchar(50) NULL;
END

-- ----- [EXP出港账单审核规则]（可空） -----
IF EXISTS (SELECT 1 FROM sys.columns c JOIN sys.types ty ON ty.user_type_id = c.user_type_id
           WHERE c.object_id = OBJECT_ID(N'[dbo].[EXP出港账单审核规则]') AND c.name = N'F业务对象ID' AND ty.name = N'bigint')
BEGIN
    PRINT N'[EXP出港账单审核规则] F业务对象ID: bigint → nvarchar(50) NULL';
    ALTER TABLE [dbo].[EXP出港账单审核规则] ALTER COLUMN [F业务对象ID] nvarchar(50) NULL;
END

-- ----- [EXP预付款记录] -----
IF EXISTS (SELECT 1 FROM sys.columns c JOIN sys.types ty ON ty.user_type_id = c.user_type_id
           WHERE c.object_id = OBJECT_ID(N'[dbo].[EXP预付款记录]') AND c.name = N'F业务对象ID' AND ty.name = N'bigint')
BEGIN
    PRINT N'[EXP预付款记录] F业务对象ID: bigint → nvarchar(50) NOT NULL';
    ALTER TABLE [dbo].[EXP预付款记录] ALTER COLUMN [F业务对象ID] nvarchar(50) NOT NULL;
END

-- ========== Step 2: 有索引依赖的表（drop 索引 → 改列 → 原样重建） ==========

-- ----- [EXP费用减免] -----
IF EXISTS (SELECT 1 FROM sys.columns c JOIN sys.types ty ON ty.user_type_id = c.user_type_id
           WHERE c.object_id = OBJECT_ID(N'[dbo].[EXP费用减免]') AND c.name = N'F业务对象ID' AND ty.name = N'bigint')
BEGIN
    PRINT N'[EXP费用减免] F业务对象ID: bigint → nvarchar(50) NOT NULL（重建 IX_EXP费用减免_业务对象启用）';
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_EXP费用减免_业务对象启用' AND object_id = OBJECT_ID(N'[dbo].[EXP费用减免]'))
        DROP INDEX [IX_EXP费用减免_业务对象启用] ON [dbo].[EXP费用减免];
    ALTER TABLE [dbo].[EXP费用减免] ALTER COLUMN [F业务对象ID] nvarchar(50) NOT NULL;
    CREATE NONCLUSTERED INDEX [IX_EXP费用减免_业务对象启用] ON [dbo].[EXP费用减免] ([F业务对象ID] ASC, [F启用] ASC);
END

-- ----- [EXP均重上限] -----
IF EXISTS (SELECT 1 FROM sys.columns c JOIN sys.types ty ON ty.user_type_id = c.user_type_id
           WHERE c.object_id = OBJECT_ID(N'[dbo].[EXP均重上限]') AND c.name = N'F业务对象ID' AND ty.name = N'bigint')
BEGIN
    PRINT N'[EXP均重上限] F业务对象ID: bigint → nvarchar(50) NOT NULL（重建 IX_EXP均重上限_业务对象品牌启用）';
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_EXP均重上限_业务对象品牌启用' AND object_id = OBJECT_ID(N'[dbo].[EXP均重上限]'))
        DROP INDEX [IX_EXP均重上限_业务对象品牌启用] ON [dbo].[EXP均重上限];
    ALTER TABLE [dbo].[EXP均重上限] ALTER COLUMN [F业务对象ID] nvarchar(50) NOT NULL;
    CREATE NONCLUSTERED INDEX [IX_EXP均重上限_业务对象品牌启用] ON [dbo].[EXP均重上限] ([F业务对象ID] ASC, [F品牌编码] ASC, [F启用] ASC);
END

-- ----- [EXP客户返利] -----
IF EXISTS (SELECT 1 FROM sys.columns c JOIN sys.types ty ON ty.user_type_id = c.user_type_id
           WHERE c.object_id = OBJECT_ID(N'[dbo].[EXP客户返利]') AND c.name = N'F业务对象ID' AND ty.name = N'bigint')
BEGIN
    PRINT N'[EXP客户返利] F业务对象ID: bigint → nvarchar(50) NOT NULL（重建 IX_EXP客户返利_业务对象品牌启用）';
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_EXP客户返利_业务对象品牌启用' AND object_id = OBJECT_ID(N'[dbo].[EXP客户返利]'))
        DROP INDEX [IX_EXP客户返利_业务对象品牌启用] ON [dbo].[EXP客户返利];
    ALTER TABLE [dbo].[EXP客户返利] ALTER COLUMN [F业务对象ID] nvarchar(50) NOT NULL;
    CREATE NONCLUSTERED INDEX [IX_EXP客户返利_业务对象品牌启用] ON [dbo].[EXP客户返利] ([F业务对象ID] ASC, [F品牌编码] ASC, [F启用] ASC);
END

-- ----- [EXP客户运单号余额]（唯一索引） -----
IF EXISTS (SELECT 1 FROM sys.columns c JOIN sys.types ty ON ty.user_type_id = c.user_type_id
           WHERE c.object_id = OBJECT_ID(N'[dbo].[EXP客户运单号余额]') AND c.name = N'F业务对象ID' AND ty.name = N'bigint')
BEGIN
    PRINT N'[EXP客户运单号余额] F业务对象ID: bigint → nvarchar(50) NOT NULL（重建唯一索引 IX_EXP客户运单号余额_业务对象品牌）';
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_EXP客户运单号余额_业务对象品牌' AND object_id = OBJECT_ID(N'[dbo].[EXP客户运单号余额]'))
        DROP INDEX [IX_EXP客户运单号余额_业务对象品牌] ON [dbo].[EXP客户运单号余额];
    ALTER TABLE [dbo].[EXP客户运单号余额] ALTER COLUMN [F业务对象ID] nvarchar(50) NOT NULL;
    CREATE UNIQUE NONCLUSTERED INDEX [IX_EXP客户运单号余额_业务对象品牌] ON [dbo].[EXP客户运单号余额] ([F业务对象ID] ASC, [F品牌编码] ASC);
END

-- ----- [EXP目的地占比] -----
IF EXISTS (SELECT 1 FROM sys.columns c JOIN sys.types ty ON ty.user_type_id = c.user_type_id
           WHERE c.object_id = OBJECT_ID(N'[dbo].[EXP目的地占比]') AND c.name = N'F业务对象ID' AND ty.name = N'bigint')
BEGIN
    PRINT N'[EXP目的地占比] F业务对象ID: bigint → nvarchar(50) NOT NULL（重建 IX_EXP目的地占比_业务对象品牌省份启用）';
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_EXP目的地占比_业务对象品牌省份启用' AND object_id = OBJECT_ID(N'[dbo].[EXP目的地占比]'))
        DROP INDEX [IX_EXP目的地占比_业务对象品牌省份启用] ON [dbo].[EXP目的地占比];
    ALTER TABLE [dbo].[EXP目的地占比] ALTER COLUMN [F业务对象ID] nvarchar(50) NOT NULL;
    CREATE NONCLUSTERED INDEX [IX_EXP目的地占比_业务对象品牌省份启用] ON [dbo].[EXP目的地占比] ([F业务对象ID] ASC, [F品牌编码] ASC, [F省份ID] ASC, [F启用] ASC);
END

-- ----- [EXP预付款流水] -----
IF EXISTS (SELECT 1 FROM sys.columns c JOIN sys.types ty ON ty.user_type_id = c.user_type_id
           WHERE c.object_id = OBJECT_ID(N'[dbo].[EXP预付款流水]') AND c.name = N'F业务对象ID' AND ty.name = N'bigint')
BEGIN
    PRINT N'[EXP预付款流水] F业务对象ID: bigint → nvarchar(50) NOT NULL（重建 IX_EXP预付款流水_业务对象创建时间）';
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_EXP预付款流水_业务对象创建时间' AND object_id = OBJECT_ID(N'[dbo].[EXP预付款流水]'))
        DROP INDEX [IX_EXP预付款流水_业务对象创建时间] ON [dbo].[EXP预付款流水];
    ALTER TABLE [dbo].[EXP预付款流水] ALTER COLUMN [F业务对象ID] nvarchar(50) NOT NULL;
    CREATE NONCLUSTERED INDEX [IX_EXP预付款流水_业务对象创建时间] ON [dbo].[EXP预付款流水] ([F业务对象ID] ASC, [F创建时间] ASC);
END

-- ----- [EXP预付款余额]（唯一索引） -----
IF EXISTS (SELECT 1 FROM sys.columns c JOIN sys.types ty ON ty.user_type_id = c.user_type_id
           WHERE c.object_id = OBJECT_ID(N'[dbo].[EXP预付款余额]') AND c.name = N'F业务对象ID' AND ty.name = N'bigint')
BEGIN
    PRINT N'[EXP预付款余额] F业务对象ID: bigint → nvarchar(50) NOT NULL（重建唯一索引 IX_EXP预付款余额_F业务对象ID）';
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_EXP预付款余额_F业务对象ID' AND object_id = OBJECT_ID(N'[dbo].[EXP预付款余额]'))
        DROP INDEX [IX_EXP预付款余额_F业务对象ID] ON [dbo].[EXP预付款余额];
    ALTER TABLE [dbo].[EXP预付款余额] ALTER COLUMN [F业务对象ID] nvarchar(50) NOT NULL;
    CREATE UNIQUE NONCLUSTERED INDEX [IX_EXP预付款余额_F业务对象ID] ON [dbo].[EXP预付款余额] ([F业务对象ID] ASC);
END

-- ----- [EXP月度调整] -----
IF EXISTS (SELECT 1 FROM sys.columns c JOIN sys.types ty ON ty.user_type_id = c.user_type_id
           WHERE c.object_id = OBJECT_ID(N'[dbo].[EXP月度调整]') AND c.name = N'F业务对象ID' AND ty.name = N'bigint')
BEGIN
    PRINT N'[EXP月度调整] F业务对象ID: bigint → nvarchar(50) NOT NULL（重建 IX_EXP月度调整_业务对象月份）';
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_EXP月度调整_业务对象月份' AND object_id = OBJECT_ID(N'[dbo].[EXP月度调整]'))
        DROP INDEX [IX_EXP月度调整_业务对象月份] ON [dbo].[EXP月度调整];
    ALTER TABLE [dbo].[EXP月度调整] ALTER COLUMN [F业务对象ID] nvarchar(50) NOT NULL;
    CREATE NONCLUSTERED INDEX [IX_EXP月度调整_业务对象月份] ON [dbo].[EXP月度调整] ([F业务对象ID] ASC, [F月份] ASC);
END

-- ----- [EXP运单号交易] -----
IF EXISTS (SELECT 1 FROM sys.columns c JOIN sys.types ty ON ty.user_type_id = c.user_type_id
           WHERE c.object_id = OBJECT_ID(N'[dbo].[EXP运单号交易]') AND c.name = N'F业务对象ID' AND ty.name = N'bigint')
BEGIN
    PRINT N'[EXP运单号交易] F业务对象ID: bigint → nvarchar(50) NOT NULL（重建 IX_EXP运单号交易_业务对象品牌日期）';
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_EXP运单号交易_业务对象品牌日期' AND object_id = OBJECT_ID(N'[dbo].[EXP运单号交易]'))
        DROP INDEX [IX_EXP运单号交易_业务对象品牌日期] ON [dbo].[EXP运单号交易];
    ALTER TABLE [dbo].[EXP运单号交易] ALTER COLUMN [F业务对象ID] nvarchar(50) NOT NULL;
    CREATE NONCLUSTERED INDEX [IX_EXP运单号交易_业务对象品牌日期] ON [dbo].[EXP运单号交易] ([F业务对象ID] ASC, [F品牌编码] ASC, [F交易日期] ASC);
END

-- ========== Step 3: 迁移后验证（应无 bigint 残留） ==========
DECLARE @remain int;
SELECT @remain = COUNT(*)
FROM sys.columns c
JOIN sys.types ty ON ty.user_type_id = c.user_type_id
WHERE c.name = N'F业务对象ID' AND ty.name = N'bigint'
  AND OBJECT_NAME(c.object_id) IN (N'EXP出港运单_历史', N'EXP出港账单审核规则', N'EXP费用减免', N'EXP均重上限',
                                   N'EXP客户返利', N'EXP客户运单号余额', N'EXP目的地占比', N'EXP预付款记录',
                                   N'EXP预付款流水', N'EXP预付款余额', N'EXP月度调整', N'EXP运单号交易');

IF @remain <> 0
    THROW 51001, N'迁移校验失败：仍有 [F业务对象ID] 列为 bigint。', 1;

PRINT N'[完成] 12 张表 [F业务对象ID] 已全部为 nvarchar(50)，bigint 残留 = 0。';

COMMIT TRANSACTION;
PRINT N'迁移已提交。';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    PRINT N'迁移失败，已回滚。错误：' + ERROR_MESSAGE();
    THROW;
END CATCH
