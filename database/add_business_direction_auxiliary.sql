-- 为辅助核算添加"业务方向"类别及预置值
-- 执行日期: 2026-05-24

-- 1. 添加业务方向类型（如果不存在）
IF NOT EXISTS (SELECT 1 FROM [FIN辅助核算类型] WHERE [F名称] = N'business_direction')
BEGIN
    INSERT INTO [FIN辅助核算类型] ([F名称], [F状态], [F创建时间], [F更新时间])
    VALUES (N'business_direction', 1, GETDATE(), GETDATE())
END

-- 2. 添加业务方向预置值（如果不存在）
-- 获取默认账套ID和根组织ID
DECLARE @defaultAccountSetId BIGINT = 0
DECLARE @defaultOrgId BIGINT = 0

SELECT @defaultAccountSetId = ISNULL((SELECT TOP 1 FID FROM [FIN账套] WHERE [F是否默认] = 1), 0)
SELECT @defaultOrgId = ISNULL((SELECT TOP 1 FID FROM [SYS组织架构] WHERE [F父ID] = 0 OR [F父ID] IS NULL), 0)

-- 添加进港
IF NOT EXISTS (SELECT 1 FROM [FIN辅助核算项目] WHERE [F辅助类型] = N'business_direction' AND [F编码] = N'IN')
BEGIN
    INSERT INTO [FIN辅助核算项目] ([F编码], [F名称], [F辅助类型], [F启用状态], [F账套ID], [F组织ID], [F创建时间], [F更新时间])
    VALUES (N'IN', N'进港', N'business_direction', 1, @defaultAccountSetId, @defaultOrgId, GETDATE(), GETDATE())
END

-- 添加出港
IF NOT EXISTS (SELECT 1 FROM [FIN辅助核算项目] WHERE [F辅助类型] = N'business_direction' AND [F编码] = N'OUT')
BEGIN
    INSERT INTO [FIN辅助核算项目] ([F编码], [F名称], [F辅助类型], [F启用状态], [F账套ID], [F组织ID], [F创建时间], [F更新时间])
    VALUES (N'OUT', N'出港', N'business_direction', 1, @defaultAccountSetId, @defaultOrgId, GETDATE(), GETDATE())
END

-- 添加综合
IF NOT EXISTS (SELECT 1 FROM [FIN辅助核算项目] WHERE [F辅助类型] = N'business_direction' AND [F编码] = N'CMB')
BEGIN
    INSERT INTO [FIN辅助核算项目] ([F编码], [F名称], [F辅助类型], [F启用状态], [F账套ID], [F组织ID], [F创建时间], [F更新时间])
    VALUES (N'CMB', N'综合', N'business_direction', 1, @defaultAccountSetId, @defaultOrgId, GETDATE(), GETDATE())
END
