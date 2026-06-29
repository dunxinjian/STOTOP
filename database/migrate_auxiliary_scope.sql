-- ============================================
-- 辅助核算类型作用域重构迁移脚本
-- 将全局标识从项目级(F是否全局)上提到类型级(F作用域)
-- ============================================

-- Step 1: FIN辅助核算类型 表新增 F作用域 字段
IF NOT EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'[FIN辅助核算类型]') AND name = N'F作用域'
)
BEGIN
    ALTER TABLE [FIN辅助核算类型] 
    ADD [F作用域] NVARCHAR(20) NOT NULL CONSTRAINT [DF_FIN辅助核算类型_F作用域] DEFAULT N'org_scoped';
END

-- Step 2: 将 express_brand 和 business_direction 标记为 global
UPDATE [FIN辅助核算类型] SET [F作用域] = N'global' WHERE [F名称] = N'express_brand';
UPDATE [FIN辅助核算类型] SET [F作用域] = N'global' WHERE [F名称] = N'business_direction';

-- Step 3: 删除 FIN辅助核算项目 表的旧索引 IX_FIN辅助核算项目_全局类型查询
IF EXISTS (
    SELECT 1 FROM sys.indexes 
    WHERE object_id = OBJECT_ID(N'[FIN辅助核算项目]') AND name = N'IX_FIN辅助核算项目_全局类型查询'
)
BEGIN
    DROP INDEX [IX_FIN辅助核算项目_全局类型查询] ON [FIN辅助核算项目];
END

-- Step 4: 删除 FIN辅助核算项目 表的 F是否全局 列
IF EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'[FIN辅助核算项目]') AND name = N'F是否全局'
)
BEGIN
    ALTER TABLE [FIN辅助核算项目] DROP COLUMN [F是否全局];
END

-- Step 5: 新建替代索引，覆盖按辅助类型查询场景
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes 
    WHERE object_id = OBJECT_ID(N'[FIN辅助核算项目]') AND name = N'IX_FIN辅助核算项目_类型状态'
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_FIN辅助核算项目_类型状态]
    ON [FIN辅助核算项目] ([F辅助类型], [F启用状态])
    INCLUDE ([F编码], [F名称], [F账套ID], [F组织ID]);
END

-- Step 6: 验证查询（执行完成后可手动运行检查结果）
-- SELECT [F名称], [F作用域] FROM [FIN辅助核算类型] ORDER BY [F名称];
-- EXEC sp_helpindex '[FIN辅助核算项目]';
