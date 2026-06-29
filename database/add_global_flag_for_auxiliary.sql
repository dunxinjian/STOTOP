-- 为辅助核算项目添加全局标识字段
-- 执行日期: 2026-05-24
-- 说明：快递品牌(express_brand)和业务方向(business_direction)为全局共享数据，不分组织、不分账套

-- 1. 添加 F是否全局 字段
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[FIN辅助核算项目]') AND name = N'F是否全局')
BEGIN
    ALTER TABLE [FIN辅助核算项目] 
    ADD [F是否全局] BIT NOT NULL DEFAULT 0;
    
    PRINT '已添加 F是否全局 字段';
END
ELSE
BEGIN
    PRINT 'F是否全局 字段已存在';
END

-- 2. 为已有的快递品牌设置为全局
UPDATE [FIN辅助核算项目] 
SET [F是否全局] = 1,
    [F账套ID] = 0,  -- 全局数据账套ID设为0
    [F组织ID] = 0   -- 全局数据组织ID设为0
WHERE [F辅助类型] = N'express_brand' AND [F是否全局] = 0;

PRINT '已将快递品牌设置为全局数据';

-- 3. 为已有的业务方向设置为全局
UPDATE [FIN辅助核算项目] 
SET [F是否全局] = 1,
    [F账套ID] = 0,  -- 全局数据账套ID设为0
    [F组织ID] = 0   -- 全局数据组织ID设为0
WHERE [F辅助类型] = N'business_direction' AND [F是否全局] = 0;

PRINT '已将业务方向设置为全局数据';

-- 4. 添加索引优化查询性能
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_FIN辅助核算项目_全局类型查询')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_FIN辅助核算项目_全局类型查询]
    ON [FIN辅助核算项目] ([F是否全局], [F辅助类型], [F启用状态])
    INCLUDE ([F编码], [F名称], [F账套ID], [F组织ID]);
    
    PRINT '已创建索引 IX_FIN辅助核算项目_全局类型查询';
END

-- 5. 验证结果
SELECT 
    [F辅助类型],
    COUNT(*) AS [总数],
    SUM(CASE WHEN [F是否全局] = 1 THEN 1 ELSE 0 END) AS [全局数量],
    SUM(CASE WHEN [F是否全局] = 0 THEN 1 ELSE 0 END) AS [非全局数量]
FROM [FIN辅助核算项目]
GROUP BY [F辅助类型]
ORDER BY [F辅助类型];
