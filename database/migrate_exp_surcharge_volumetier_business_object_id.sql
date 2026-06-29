-- =============================================
-- EXP快递报价_出港加收 / EXP发件量阶梯 的 [F业务对象ID]: bigint → nvarchar(50) 迁移脚本
-- 范围: 两张此前被漏掉的同概念表（其余 12 张已由 migrate_exp_business_object_id_bigint_to_nvarchar.sql 处理）。
-- 背景:
--   实体 ExpPriceSurcharge / ExpVolumeTier 原把 [F业务对象ID] 误建为 long(bigint)，与其余 12 张 string(nvarchar(50)) 不一致。
--   业务对象以字符串编号为键（如 CRM客户 主键 F编号 形如 'KH00000178'，非数字），bigint 无法存储，故应为 nvarchar(50)。
--   代码侧已将两实体属性对齐为 string FClientId（映射列名仍为 F业务对象ID）。
-- 日期: 2026-06-28
-- 说明:
--   1. 幂等：每表以"列仍为 bigint"为执行守卫；改完即 no-op。
--   2. 安全前置：仅当两表 [F业务对象ID] 均无非空值时执行（EXP快递报价_出港加收 现有 15 行但该列全 NULL，EXP发件量阶梯 为空）；
--      若存在非空旧 bigint 值则中止，转字符串需人工语义复核。
--   3. 各表 [F业务对象ID] 上各有 1 个非聚集索引，按库中原定义 drop → 改列 → 原样重建。
--   4. 整脚本事务包裹（XACT_ABORT）；可空性与 EF 模型一致：EXP发件量阶梯 = NOT NULL，EXP快递报价_出港加收 = NULL。
-- =============================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
BEGIN TRANSACTION;

-- ========== Step 0: 前置检查（不得存在非空 F业务对象ID） ==========
PRINT N'[Step 0] 前置检查：确认两表 [F业务对象ID] 无非空旧值...';

DECLARE @nonNull nvarchar(2048);
SELECT @nonNull = STRING_AGG(x.T + N'(' + CAST(x.c AS nvarchar(20)) + N')', N', ')
FROM (
    SELECT N'EXP发件量阶梯' AS T, COUNT(*) AS c FROM [dbo].[EXP发件量阶梯] WHERE [F业务对象ID] IS NOT NULL
    UNION ALL
    SELECT N'EXP快递报价_出港加收', COUNT(*) FROM [dbo].[EXP快递报价_出港加收] WHERE [F业务对象ID] IS NOT NULL
) x
WHERE x.c > 0;

IF @nonNull IS NOT NULL
BEGIN
    SET @nonNull = N'迁移已中止：以下表存在非空 [F业务对象ID]（旧 bigint 值转字符串需人工语义复核）→ ' + @nonNull;
    THROW 51000, @nonNull, 1;
END

-- ========== Step 1: [EXP发件量阶梯]（NOT NULL，重建 IX_EXP发件量阶梯_业务对象品牌发件量） ==========
IF EXISTS (SELECT 1 FROM sys.columns c JOIN sys.types ty ON ty.user_type_id = c.user_type_id
           WHERE c.object_id = OBJECT_ID(N'[dbo].[EXP发件量阶梯]') AND c.name = N'F业务对象ID' AND ty.name = N'bigint')
BEGIN
    PRINT N'[EXP发件量阶梯] F业务对象ID: bigint → nvarchar(50) NOT NULL（重建 IX_EXP发件量阶梯_业务对象品牌发件量）';
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_EXP发件量阶梯_业务对象品牌发件量' AND object_id = OBJECT_ID(N'[dbo].[EXP发件量阶梯]'))
        DROP INDEX [IX_EXP发件量阶梯_业务对象品牌发件量] ON [dbo].[EXP发件量阶梯];
    ALTER TABLE [dbo].[EXP发件量阶梯] ALTER COLUMN [F业务对象ID] nvarchar(50) NOT NULL;
    CREATE NONCLUSTERED INDEX [IX_EXP发件量阶梯_业务对象品牌发件量] ON [dbo].[EXP发件量阶梯] ([F业务对象ID] ASC, [F品牌编码] ASC, [F最低月发件量] ASC);
END

-- ========== Step 2: [EXP快递报价_出港加收]（NULL，重建 IX_EXP快递报价_出港加收_业务对象品牌启用） ==========
IF EXISTS (SELECT 1 FROM sys.columns c JOIN sys.types ty ON ty.user_type_id = c.user_type_id
           WHERE c.object_id = OBJECT_ID(N'[dbo].[EXP快递报价_出港加收]') AND c.name = N'F业务对象ID' AND ty.name = N'bigint')
BEGIN
    PRINT N'[EXP快递报价_出港加收] F业务对象ID: bigint → nvarchar(50) NULL（重建 IX_EXP快递报价_出港加收_业务对象品牌启用）';
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_EXP快递报价_出港加收_业务对象品牌启用' AND object_id = OBJECT_ID(N'[dbo].[EXP快递报价_出港加收]'))
        DROP INDEX [IX_EXP快递报价_出港加收_业务对象品牌启用] ON [dbo].[EXP快递报价_出港加收];
    ALTER TABLE [dbo].[EXP快递报价_出港加收] ALTER COLUMN [F业务对象ID] nvarchar(50) NULL;
    CREATE NONCLUSTERED INDEX [IX_EXP快递报价_出港加收_业务对象品牌启用] ON [dbo].[EXP快递报价_出港加收] ([F业务对象ID] ASC, [F品牌编码] ASC, [F启用] ASC);
END

-- ========== Step 3: 迁移后验证（应无 bigint 残留） ==========
DECLARE @remain int;
SELECT @remain = COUNT(*)
FROM sys.columns c
JOIN sys.types ty ON ty.user_type_id = c.user_type_id
WHERE c.name = N'F业务对象ID' AND ty.name = N'bigint'
  AND OBJECT_NAME(c.object_id) IN (N'EXP发件量阶梯', N'EXP快递报价_出港加收');

IF @remain <> 0
    THROW 51001, N'迁移校验失败：仍有 [F业务对象ID] 列为 bigint。', 1;

PRINT N'[完成] 两表 [F业务对象ID] 已为 nvarchar(50)，bigint 残留 = 0。';

COMMIT TRANSACTION;
PRINT N'迁移已提交。';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    PRINT N'迁移失败，已回滚。错误：' + ERROR_MESSAGE();
    THROW;
END CATCH
