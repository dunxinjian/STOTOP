-- =============================================
-- DC 管道实例 → CardFlow 卡片流程 迁移脚本
-- 范围: 组织 192，DC管道定义 FID = 9 / 10 / 11
-- 目标: 在 [CF卡片流程] / [CF流程版本] / [CF流程节点] 中重建对应流程
-- 日期: 2026-05-25
-- 说明:
--   1. 整脚本事务包裹，幂等（以 [F流程编码]+[F组织ID] 为唯一键）
--   2. 重复执行时，已存在的流程会被跳过；首次执行会创建 3 个流程 / 3 个版本 / 10 个节点
--   3. F创建人ID = 1（系统管理员）；F组织ID = 192
-- =============================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
BEGIN TRANSACTION;

-- ========== Step 0: 前置检查 + 补充插件注册 ==========
PRINT N'[Step 0] 前置检查...';

-- 0.1 检查源数据是否存在（DC 管道定义/Agent 表，及目标表是否就绪）
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'CF自动插件注册')
    THROW 50001, N'目标表 [CF自动插件注册] 不存在，请先确保 SchemaAutoSync 已建表。', 1;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'CF卡片流程')
    THROW 50002, N'目标表 [CF卡片流程] 不存在，请先确保 SchemaAutoSync 已建表。', 1;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'CF流程版本')
    THROW 50003, N'目标表 [CF流程版本] 不存在，请先确保 SchemaAutoSync 已建表。', 1;
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'CF流程节点')
    THROW 50004, N'目标表 [CF流程节点] 不存在，请先确保 SchemaAutoSync 已建表。', 1;

-- DC 源表为可选（可能已被清理），仅做提示性检查
DECLARE @hasDcPipeline BIT = CASE WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'DC管道定义') THEN 1 ELSE 0 END;
DECLARE @hasDcAgent    BIT = CASE WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'DC管道Agent') THEN 1 ELSE 0 END;

IF @hasDcPipeline = 1
BEGIN
    DECLARE @cnt INT;
    SELECT @cnt = COUNT(*) FROM [DC管道定义] WHERE [FID] IN (9, 10, 11);
    PRINT N'[Step 0] 源 DC管道定义 命中行数: ' + CAST(@cnt AS NVARCHAR(10));
END
ELSE
BEGIN
    PRINT N'[Step 0] 警告: 源表 [DC管道定义] 已不存在，迁移将依据脚本内常量执行（与原始数据一致）。';
END

IF @hasDcAgent = 1
BEGIN
    DECLARE @agentCnt INT;
    SELECT @agentCnt = COUNT(*) FROM [DC管道Agent] WHERE [FID] IN (29, 30, 31, 32, 33, 34, 35, 36, 37, 39);
    PRINT N'[Step 0] 源 DC管道Agent 命中行数: ' + CAST(@agentCnt AS NVARCHAR(10));
END

-- 0.2 校验已存在的插件注册项（ExcelInput / QualityAnalysis / AutoVoucher 应已由 CardFlowSeeder V13 创建）
IF NOT EXISTS (SELECT 1 FROM [CF自动插件注册] WHERE [F插件编码] IN (N'ExcelInput', N'QualityAnalysis', N'AutoVoucher'))
    THROW 50010, N'缺少基础插件（ExcelInput/QualityAnalysis/AutoVoucher）注册，请先运行 CardFlowSeeder V13。', 1;

-- 0.3 补充 Pricing / Cost 插件（幂等）
;WITH plugins([F插件编码],[F插件名称],[F插件类型],[F处理粒度],[F说明]) AS (
    SELECT N'Pricing', N'计价计算', N'Processing', N'batch', N'按规则计算运单收入/成本（出港运单价格计算等）' UNION ALL
    SELECT N'Cost',    N'成本计算', N'Processing', N'batch', N'按成本方案计算运单/批次成本'
)
INSERT INTO [CF自动插件注册] ([F插件编码], [F插件名称], [F插件类型], [F处理粒度], [F说明], [F状态])
SELECT p.[F插件编码], p.[F插件名称], p.[F插件类型], p.[F处理粒度], p.[F说明], 1
FROM plugins p
WHERE NOT EXISTS (SELECT 1 FROM [CF自动插件注册] r WHERE r.[F插件编码] = p.[F插件编码]);

PRINT N'[Step 0] 插件注册补齐完成 (Pricing / Cost)。';

-- 0.4 解析插件注册 ID 到变量（后续节点插入复用）
DECLARE @pidExcelInput      BIGINT = (SELECT [FID] FROM [CF自动插件注册] WHERE [F插件编码] = N'ExcelInput');
DECLARE @pidQualityAnalysis BIGINT = (SELECT [FID] FROM [CF自动插件注册] WHERE [F插件编码] = N'QualityAnalysis');
DECLARE @pidAutoVoucher     BIGINT = (SELECT [FID] FROM [CF自动插件注册] WHERE [F插件编码] = N'AutoVoucher');
DECLARE @pidPricing         BIGINT = (SELECT [FID] FROM [CF自动插件注册] WHERE [F插件编码] = N'Pricing');
DECLARE @pidCost            BIGINT = (SELECT [FID] FROM [CF自动插件注册] WHERE [F插件编码] = N'Cost');

IF @pidExcelInput IS NULL OR @pidQualityAnalysis IS NULL OR @pidAutoVoucher IS NULL
   OR @pidPricing IS NULL OR @pidCost IS NULL
    THROW 50011, N'解析插件注册 ID 失败，存在 NULL，请检查 [CF自动插件注册] 数据。', 1;

-- ========== 流程级常量 ==========
DECLARE @orgId         BIGINT       = 192;
DECLARE @creatorId     BIGINT       = 1;
DECLARE @now           DATETIME2(7) = SYSUTCDATETIME();
DECLARE @triggerJson   NVARCHAR(MAX) = N'{"type":"fileUpload"}';
DECLARE @flowSettings  NVARCHAR(MAX) = N'{"source":"DcPipelineMigration","sourcePipelineId":__PID__}';

-- 用于接收 INSERT...OUTPUT 的临时表
DECLARE @flowOut    TABLE ([FID] BIGINT, [F流程编码] NVARCHAR(50));
DECLARE @versionOut TABLE ([FID] BIGINT, [F流程定义ID] BIGINT);

-- ========== Step 1: 创建卡片流程定义 (CF卡片流程) ==========
PRINT N'[Step 1] 创建 CF卡片流程 ...';

-- ---- 流程 A: 申通出港运单导入 (来源 DC管道定义.FID=9) ----
IF NOT EXISTS (SELECT 1 FROM [CF卡片流程]
               WHERE [F流程编码] = N'PL_ST_OUTBOUND_WAYBILL' AND [F组织ID] = @orgId)
BEGIN
    INSERT INTO [CF卡片流程]
        ([F流程名称], [F流程编码], [F描述], [F状态],
         [F编号模板], [F标题模板], [F可发起角色JSON], [F流程组ID],
         [F组织ID], [F账套ID], [F触发配置JSON], [F创建人ID], [F创建时间], [F更新时间])
    OUTPUT INSERTED.[FID], INSERTED.[F流程编码] INTO @flowOut([FID], [F流程编码])
    VALUES
        (N'申通出港运单导入', N'PL_ST_OUTBOUND_WAYBILL',
         N'迁移自 DC管道(FID=9)：Excel导入 → 质量分析 → 出港运单价格计算 → 出港运单成本计算',
         N'published',
         NULL, NULL, NULL, NULL,
         @orgId, NULL, @triggerJson, @creatorId, @now, NULL);
END

-- ---- 流程 B: 申通总部交易明细导入 (来源 DC管道定义.FID=10) ----
IF NOT EXISTS (SELECT 1 FROM [CF卡片流程]
               WHERE [F流程编码] = N'PL_ST_HQ_TRANSACTION' AND [F组织ID] = @orgId)
BEGIN
    INSERT INTO [CF卡片流程]
        ([F流程名称], [F流程编码], [F描述], [F状态],
         [F编号模板], [F标题模板], [F可发起角色JSON], [F流程组ID],
         [F组织ID], [F账套ID], [F触发配置JSON], [F创建人ID], [F创建时间], [F更新时间])
    OUTPUT INSERTED.[FID], INSERTED.[F流程编码] INTO @flowOut([FID], [F流程编码])
    VALUES
        (N'申通总部交易明细导入', N'PL_ST_HQ_TRANSACTION',
         N'迁移自 DC管道(FID=10)：Excel导入 → 质量分析 → 自动凭证',
         N'published',
         NULL, NULL, NULL, NULL,
         @orgId, NULL, @triggerJson, @creatorId, @now, NULL);
END

-- ---- 流程 C: 费用支出导入 (来源 DC管道定义.FID=11) ----
IF NOT EXISTS (SELECT 1 FROM [CF卡片流程]
               WHERE [F流程编码] = N'PL_EXPENSE_REIMBURSE' AND [F组织ID] = @orgId)
BEGIN
    INSERT INTO [CF卡片流程]
        ([F流程名称], [F流程编码], [F描述], [F状态],
         [F编号模板], [F标题模板], [F可发起角色JSON], [F流程组ID],
         [F组织ID], [F账套ID], [F触发配置JSON], [F创建人ID], [F创建时间], [F更新时间])
    OUTPUT INSERTED.[FID], INSERTED.[F流程编码] INTO @flowOut([FID], [F流程编码])
    VALUES
        (N'费用支出导入', N'PL_EXPENSE_REIMBURSE',
         N'迁移自 DC管道(FID=11)：Excel导入 → 质量分析 → 自动凭证生成-费用支出凭证规则',
         N'published',
         NULL, NULL, NULL, NULL,
         @orgId, NULL, @triggerJson, @creatorId, @now, NULL);
END

-- 解析三个流程定义 ID（兼容首次执行 / 重复执行：从表里直接拿）
DECLARE @flowAId BIGINT = (SELECT [FID] FROM [CF卡片流程] WHERE [F流程编码] = N'PL_ST_OUTBOUND_WAYBILL' AND [F组织ID] = @orgId);
DECLARE @flowBId BIGINT = (SELECT [FID] FROM [CF卡片流程] WHERE [F流程编码] = N'PL_ST_HQ_TRANSACTION'   AND [F组织ID] = @orgId);
DECLARE @flowCId BIGINT = (SELECT [FID] FROM [CF卡片流程] WHERE [F流程编码] = N'PL_EXPENSE_REIMBURSE'    AND [F组织ID] = @orgId);

IF @flowAId IS NULL OR @flowBId IS NULL OR @flowCId IS NULL
    THROW 50020, N'解析流程定义 ID 失败：CF卡片流程 中未找到迁移流程，请检查 Step 1。', 1;

PRINT N'[Step 1] 流程定义 OK. flowA=' + CAST(@flowAId AS NVARCHAR(20))
    + N', flowB=' + CAST(@flowBId AS NVARCHAR(20))
    + N', flowC=' + CAST(@flowCId AS NVARCHAR(20));

-- ========== Step 2: 创建流程版本 (CF流程版本, V1, published, 当前版本) ==========
PRINT N'[Step 2] 创建 CF流程版本 ...';

IF NOT EXISTS (SELECT 1 FROM [CF流程版本] WHERE [F流程定义ID] = @flowAId)
BEGIN
    INSERT INTO [CF流程版本]
        ([F流程定义ID], [F版本号], [F状态],
         [F卡片SchemaJSON], [F明细SchemaJSON], [F流程设置JSON],
         [F创建人ID], [F创建时间], [F发布时间], [F是否当前版本])
    VALUES
        (@flowAId, 1, N'published',
         NULL, NULL, REPLACE(@flowSettings, N'__PID__', N'9'),
         @creatorId, @now, @now, 1);
END

IF NOT EXISTS (SELECT 1 FROM [CF流程版本] WHERE [F流程定义ID] = @flowBId)
BEGIN
    INSERT INTO [CF流程版本]
        ([F流程定义ID], [F版本号], [F状态],
         [F卡片SchemaJSON], [F明细SchemaJSON], [F流程设置JSON],
         [F创建人ID], [F创建时间], [F发布时间], [F是否当前版本])
    VALUES
        (@flowBId, 1, N'published',
         NULL, NULL, REPLACE(@flowSettings, N'__PID__', N'10'),
         @creatorId, @now, @now, 1);
END

IF NOT EXISTS (SELECT 1 FROM [CF流程版本] WHERE [F流程定义ID] = @flowCId)
BEGIN
    INSERT INTO [CF流程版本]
        ([F流程定义ID], [F版本号], [F状态],
         [F卡片SchemaJSON], [F明细SchemaJSON], [F流程设置JSON],
         [F创建人ID], [F创建时间], [F发布时间], [F是否当前版本])
    VALUES
        (@flowCId, 1, N'published',
         NULL, NULL, REPLACE(@flowSettings, N'__PID__', N'11'),
         @creatorId, @now, @now, 1);
END

DECLARE @verAId BIGINT = (SELECT TOP 1 [FID] FROM [CF流程版本] WHERE [F流程定义ID] = @flowAId AND [F是否当前版本] = 1 ORDER BY [F版本号] DESC);
DECLARE @verBId BIGINT = (SELECT TOP 1 [FID] FROM [CF流程版本] WHERE [F流程定义ID] = @flowBId AND [F是否当前版本] = 1 ORDER BY [F版本号] DESC);
DECLARE @verCId BIGINT = (SELECT TOP 1 [FID] FROM [CF流程版本] WHERE [F流程定义ID] = @flowCId AND [F是否当前版本] = 1 ORDER BY [F版本号] DESC);

IF @verAId IS NULL OR @verBId IS NULL OR @verCId IS NULL
    THROW 50030, N'解析流程版本 ID 失败：CF流程版本 中未找到当前版本，请检查 Step 2。', 1;

PRINT N'[Step 2] 流程版本 OK. verA=' + CAST(@verAId AS NVARCHAR(20))
    + N', verB=' + CAST(@verBId AS NVARCHAR(20))
    + N', verC=' + CAST(@verCId AS NVARCHAR(20));

-- ========== Step 3: 创建流程节点 (CF流程节点) ==========
PRINT N'[Step 3] 创建 CF流程节点 ...';

-- 节点统一属性：F类型=auto / F处理粒度=batch / F审批模式=none
-- 排序号采用重新编号（1..N），避免 DC 源数据中重复排序号问题。

-- ---- 流程 A: 申通出港运单导入 (4 节点) ----
IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [F流程版本ID] = @verAId)
BEGIN
    INSERT INTO [CF流程节点]
        ([F流程版本ID], [F排序号], [F节点名称], [F类型], [F处理粒度], [F审批模式],
         [F插件注册ID], [F插件规则ID],
         [F自动Agent名称], [FAgent配置JSON],
         [F失败策略JSON], [F进入条件JSON],
         [F处理人策略], [F处理人配置JSON], [F抄送配置JSON],
         [F超时小时数], [F优先级模板], [F补充字段JSON])
    VALUES
        (@verAId, 1, N'Excel 文件导入-申通出港运单数据', N'auto', N'batch', N'none',
         @pidExcelInput,      18,   NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
        (@verAId, 2, N'质量分析',                          N'auto', N'batch', N'none',
         @pidQualityAnalysis, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
        (@verAId, 3, N'出港运单价格计算',                  N'auto', N'batch', N'none',
         @pidPricing,         19,   NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
        (@verAId, 4, N'出港运单成本计算',                  N'auto', N'batch', N'none',
         @pidCost,            24,   NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
END

-- ---- 流程 B: 申通总部交易明细导入 (3 节点) ----
IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [F流程版本ID] = @verBId)
BEGIN
    INSERT INTO [CF流程节点]
        ([F流程版本ID], [F排序号], [F节点名称], [F类型], [F处理粒度], [F审批模式],
         [F插件注册ID], [F插件规则ID],
         [F自动Agent名称], [FAgent配置JSON],
         [F失败策略JSON], [F进入条件JSON],
         [F处理人策略], [F处理人配置JSON], [F抄送配置JSON],
         [F超时小时数], [F优先级模板], [F补充字段JSON])
    VALUES
        (@verBId, 1, N'Excel 文件导入-申通总部交易明细', N'auto', N'batch', N'none',
         @pidExcelInput,      20,   NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
        (@verBId, 2, N'质量分析',                         N'auto', N'batch', N'none',
         @pidQualityAnalysis, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
        (@verBId, 3, N'自动凭证',                         N'auto', N'batch', N'none',
         @pidAutoVoucher,     21,   NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
END

-- ---- 流程 C: 费用支出导入 (3 节点) ----
IF NOT EXISTS (SELECT 1 FROM [CF流程节点] WHERE [F流程版本ID] = @verCId)
BEGIN
    INSERT INTO [CF流程节点]
        ([F流程版本ID], [F排序号], [F节点名称], [F类型], [F处理粒度], [F审批模式],
         [F插件注册ID], [F插件规则ID],
         [F自动Agent名称], [FAgent配置JSON],
         [F失败策略JSON], [F进入条件JSON],
         [F处理人策略], [F处理人配置JSON], [F抄送配置JSON],
         [F超时小时数], [F优先级模板], [F补充字段JSON])
    VALUES
        (@verCId, 1, N'Excel 文件导入-费用报销付款记录', N'auto', N'batch', N'none',
         @pidExcelInput,      22,   NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
        (@verCId, 2, N'费用支出记录质量分析',             N'auto', N'batch', N'none',
         @pidQualityAnalysis, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
        (@verCId, 3, N'自动凭证生成-费用支出凭证规则',   N'auto', N'batch', N'none',
         @pidAutoVoucher,     23,   NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
END

-- ========== 完成校验 ==========
DECLARE @totalFlows    INT = (SELECT COUNT(*) FROM [CF卡片流程]   WHERE [F组织ID] = @orgId AND [F流程编码] IN (N'PL_ST_OUTBOUND_WAYBILL', N'PL_ST_HQ_TRANSACTION', N'PL_EXPENSE_REIMBURSE'));
DECLARE @totalVersions INT = (SELECT COUNT(*) FROM [CF流程版本]   WHERE [F流程定义ID] IN (@flowAId, @flowBId, @flowCId));
DECLARE @totalStages   INT = (SELECT COUNT(*) FROM [CF流程节点]   WHERE [F流程版本ID] IN (@verAId, @verBId, @verCId));

PRINT N'[完成] 流程数=' + CAST(@totalFlows AS NVARCHAR(10))
    + N', 版本数=' + CAST(@totalVersions AS NVARCHAR(10))
    + N', 节点数=' + CAST(@totalStages AS NVARCHAR(10));
PRINT N'迁移完成: 期望 3 个流程, 3 个版本, 10 个节点';

COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    DECLARE @errMsg NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @errLine INT = ERROR_LINE();
    PRINT N'ERROR (line ' + CAST(@errLine AS NVARCHAR(10)) + N'): ' + @errMsg;
    THROW;
END CATCH
