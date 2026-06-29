using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;

namespace STOTOP.WebAPI.Data.Seeders;

public static class OASeeder
{
    private const string Module = "OA";

    public static void Migrate(STOTOPDbContext ctx)
    {
        MigrationRunner.RunMigrations(ctx, Module, new List<MigrationStep>
        {
            new(1, "OA表结构初始化", MigrateV1),
            new(2, "废除BPM历史表", MigrateV2),
            new(3, "下线OA单据体系", MigrateV3),
        });
    }

    private static void MigrateV1(STOTOPDbContext ctx)
    {
        DropOaTables(ctx);
    }

    public static void EnsureOATables(STOTOPDbContext context)
    {
        if (!SeederHelper.IsSqlServer(context)) return;

        // 5. OA审批委托
        context.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'OA审批委托')
BEGIN
    CREATE TABLE dbo.OA审批委托 (
        FID            BIGINT PRIMARY KEY IDENTITY(1,1),
        F委托人ID      BIGINT NOT NULL,
        F受托人ID      BIGINT NOT NULL,
        F组织ID        BIGINT NOT NULL,
        F流程类型       NVARCHAR(50) NULL,
        F生效日期       DATE NOT NULL,
        F失效日期       DATE NOT NULL,
        F委托原因       NVARCHAR(200) NULL,
        F状态          INT NOT NULL DEFAULT 1,
        F创建时间       DATETIME2 NOT NULL DEFAULT GETDATE()
    );
    CREATE INDEX IX_委托_委托人 ON dbo.OA审批委托(F委托人ID, F状态);
    CREATE INDEX IX_委托_受托人 ON dbo.OA审批委托(F受托人ID, F状态);
END
");

        // 6. OA费用类型
        context.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'OA费用类型')
BEGIN
    CREATE TABLE dbo.OA费用类型 (
        FID            BIGINT PRIMARY KEY IDENTITY(1,1),
        F类型编码       NVARCHAR(30) NOT NULL,
        F类型名称       NVARCHAR(50) NOT NULL,
        F适用场景       NVARCHAR(20) NOT NULL,
        F组织ID        BIGINT NOT NULL,
        F排序          INT NOT NULL DEFAULT 0,
        F是否启用       INT NOT NULL DEFAULT 1,
        F创建时间       DATETIME2 NOT NULL DEFAULT GETDATE(),
        CONSTRAINT UQ_费用类型 UNIQUE (F类型编码, F组织ID)
    );
END
");

        // 7. OA费用类型科目映射
        context.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'OA费用类型科目映射')
BEGIN
    CREATE TABLE dbo.OA费用类型科目映射 (
        FID            BIGINT PRIMARY KEY IDENTITY(1,1),
        F费用类型ID    BIGINT NOT NULL,
        F科目ID        BIGINT NOT NULL,
        F科目编码       NVARCHAR(30) NOT NULL,
        F科目名称       NVARCHAR(100) NOT NULL,
        F组织ID        BIGINT NOT NULL,
        F是否默认       INT NOT NULL DEFAULT 1
    );
    CREATE INDEX IX_类型科目_组织 ON dbo.OA费用类型科目映射(F组织ID, F费用类型ID);
END
");

            // 9. OA费用请款单
            context.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'OA费用请款单')
BEGIN
    CREATE TABLE dbo.OA费用请款单 (
        FID            BIGINT PRIMARY KEY IDENTITY(1,1),
        F单据编号       NVARCHAR(30) NOT NULL,
        F申请人ID       BIGINT NOT NULL,
        F部门ID        BIGINT NOT NULL,
        F组织ID        BIGINT NOT NULL,
        F请款事由       NVARCHAR(500) NOT NULL,
        F请款金额       DECIMAL(18,2) NOT NULL,
        F期望付款日期    DATE NULL,
        F费用类型       NVARCHAR(50) NOT NULL,
        F收款方名称     NVARCHAR(200) NULL,
        F收款方账号     NVARCHAR(50) NULL,
        F收款方开户行   NVARCHAR(200) NULL,
        F备注          NVARCHAR(500) NULL,
        F单据状态       INT NOT NULL DEFAULT 0,
        F已引用金额     DECIMAL(18,2) NOT NULL DEFAULT 0,
        F创建时间       DATETIME2 NOT NULL DEFAULT GETDATE(),
        F修改时间       DATETIME2 NULL,
        CONSTRAINT UQ_请款单编号 UNIQUE (F单据编号)
    );
    CREATE INDEX IX_请款单_组织状态 ON dbo.OA费用请款单(F组织ID, F单据状态);
END
");

            // 10. OA借款申请单
            context.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'OA借款申请单')
BEGIN
    CREATE TABLE dbo.OA借款申请单 (
        FID            BIGINT PRIMARY KEY IDENTITY(1,1),
        F单据编号       NVARCHAR(30) NOT NULL,
        F申请人PID       BIGINT NOT NULL,
        F部门ID        BIGINT NOT NULL,
        F组织ID        BIGINT NOT NULL,
        F借款金额       DECIMAL(18,2) NOT NULL,
        F借款事由       NVARCHAR(500) NOT NULL,
        F预计归还日期    DATE NULL,
        F收款方式       NVARCHAR(20) NOT NULL,
        F收款人名称     NVARCHAR(200) NOT NULL,
        F收款人账号     NVARCHAR(50) NULL,
        F收款人开户行   NVARCHAR(200) NULL,
        F备注          NVARCHAR(500) NULL,
        F单据状态       INT NOT NULL DEFAULT 0,
        F已报销冲抵金额 DECIMAL(18,2) NOT NULL DEFAULT 0,
        F已还款金额     DECIMAL(18,2) NOT NULL DEFAULT 0,
        F未核销余额     AS (F借款金额 - F已报销冲抵金额 - F已还款金额) PERSISTED,
        F凭证ID        BIGINT NULL,
        F创建时间       DATETIME2 NOT NULL DEFAULT GETDATE(),
        F修改时间       DATETIME2 NULL,
        CONSTRAINT UQ_借款申请编号 UNIQUE (F单据编号)
    );
    CREATE INDEX IX_借款申请_组织 ON dbo.OA借款申请单(F组织ID, F单据状态);
END
");

            // 11. OA费用报销单
            context.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'OA费用报销单')
BEGIN
    CREATE TABLE dbo.OA费用报销单 (
        FID            BIGINT PRIMARY KEY IDENTITY(1,1),
        F单据编号       NVARCHAR(30) NOT NULL,
        F申请人ID       BIGINT NOT NULL,
        F部门ID        BIGINT NOT NULL,
        F组织ID        BIGINT NOT NULL,
        F报销事由       NVARCHAR(500) NOT NULL,
        F报销总金额     DECIMAL(18,2) NOT NULL,
        F引用请款单ID   BIGINT NULL,
        F引用借款单ID   BIGINT NULL,
        F收款方式       NVARCHAR(20) NOT NULL,
        F收款人名称     NVARCHAR(200) NOT NULL,
        F收款人账号     NVARCHAR(50) NOT NULL,
        F收款人开户行   NVARCHAR(200) NULL,
        F附件数        INT NOT NULL DEFAULT 0,
        F备注          NVARCHAR(500) NULL,
        F单据状态       INT NOT NULL DEFAULT 0,
        F凭证ID        BIGINT NULL,
        F创建时间       DATETIME2 NOT NULL DEFAULT GETDATE(),
        F修改时间       DATETIME2 NULL,
        CONSTRAINT UQ_报销单编号 UNIQUE (F单据编号)
    );
    CREATE INDEX IX_报销单_组织状态 ON dbo.OA费用报销单(F组织ID, F单据状态);
END
");

            // 12. OA费用报销单_明细
            context.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'OA费用报销单_明细')
BEGIN
    CREATE TABLE dbo.OA费用报销单_明细 (
        FID            BIGINT PRIMARY KEY IDENTITY(1,1),
        F报销单ID       BIGINT NOT NULL,
        F行号          INT NOT NULL,
        F费用类型       NVARCHAR(50) NOT NULL,
        F费用科目编码    NVARCHAR(30) NULL,
        F摘要          NVARCHAR(200) NOT NULL,
        F金额          DECIMAL(18,2) NOT NULL,
        F发生日期       DATE NOT NULL,
        F备注          NVARCHAR(200) NULL
    );
    CREATE INDEX IX_报销明细_主表 ON dbo.OA费用报销单_明细(F报销单ID);
END
");

            // 13. OA对外付款单
            context.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'OA对外付款单')
BEGIN
    CREATE TABLE dbo.OA对外付款单 (
        FID            BIGINT PRIMARY KEY IDENTITY(1,1),
        F单据编号       NVARCHAR(30) NOT NULL,
        F申请人ID       BIGINT NOT NULL,
        F部门ID        BIGINT NOT NULL,
        F组织ID        BIGINT NOT NULL,
        F付款事由       NVARCHAR(500) NOT NULL,
        F付款总金额     DECIMAL(18,2) NOT NULL,
        F引用请款单ID   BIGINT NULL,
        F收款方名称     NVARCHAR(200) NOT NULL,
        F收款方账号     NVARCHAR(50) NOT NULL,
        F收款方开户行   NVARCHAR(200) NOT NULL,
        F付款方式       NVARCHAR(20) NOT NULL,
        F期望付款日期    DATE NULL,
        F合同编号       NVARCHAR(50) NULL,
        F发票号        NVARCHAR(50) NULL,
        F附件数        INT NOT NULL DEFAULT 0,
        F备注          NVARCHAR(500) NULL,
        F单据状态       INT NOT NULL DEFAULT 0,
        F凭证ID        BIGINT NULL,
        F创建时间       DATETIME2 NOT NULL DEFAULT GETDATE(),
        F修改时间       DATETIME2 NULL,
        CONSTRAINT UQ_付款单编号 UNIQUE (F单据编号)
    );
    CREATE INDEX IX_付款单_组织状态 ON dbo.OA对外付款单(F组织ID, F单据状态);
END
");

            // 14. OA对外付款单_明细
            context.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'OA对外付款单_明细')
BEGIN
    CREATE TABLE dbo.OA对外付款单_明细 (
        FID            BIGINT PRIMARY KEY IDENTITY(1,1),
        F付款单ID       BIGINT NOT NULL,
        F行号          INT NOT NULL,
        F费用类型       NVARCHAR(50) NOT NULL,
        F费用科目编码    NVARCHAR(30) NULL,
        F摘要          NVARCHAR(200) NOT NULL,
        F金额          DECIMAL(18,2) NOT NULL,
        F备注          NVARCHAR(200) NULL
    );
    CREATE INDEX IX_付款明细_主表 ON dbo.OA对外付款单_明细(F付款单ID);
END
");

            // 15. OA备用金申请单
            context.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'OA备用金申请单')
BEGIN
    CREATE TABLE dbo.OA备用金申请单 (
        FID            BIGINT PRIMARY KEY IDENTITY(1,1),
        F单据编号       NVARCHAR(30) NOT NULL,
        F申请人ID       BIGINT NOT NULL,
        F部门ID        BIGINT NOT NULL,
        F组织ID        BIGINT NOT NULL,
        F申请金额       DECIMAL(18,2) NOT NULL,
        F申请事由       NVARCHAR(500) NOT NULL,
        F预计归还日期    DATE NULL,
        F收款方式       NVARCHAR(20) NOT NULL,
        F收款人名称     NVARCHAR(200) NOT NULL,
        F收款人账号     NVARCHAR(50) NULL,
        F收款人开户行   NVARCHAR(200) NULL,
        F备注          NVARCHAR(500) NULL,
        F单据状态       INT NOT NULL DEFAULT 0,
        F已报销金额     DECIMAL(18,2) NOT NULL DEFAULT 0,
        F已还款金额     DECIMAL(18,2) NOT NULL DEFAULT 0,
        F未核销余额     AS (F申请金额 - F已报销金额 - F已还款金额) PERSISTED,
        F凭证ID        BIGINT NULL,
        F创建时间       DATETIME2 NOT NULL DEFAULT GETDATE(),
        CONSTRAINT UQ_备用金申请编号 UNIQUE (F单据编号)
    );
END
");

            // 16. OA备用金报销单
            context.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'OA备用金报销单')
BEGIN
    CREATE TABLE dbo.OA备用金报销单 (
        FID            BIGINT PRIMARY KEY IDENTITY(1,1),
        F单据编号       NVARCHAR(30) NOT NULL,
        F申请人ID       BIGINT NOT NULL,
        F部门ID        BIGINT NOT NULL,
        F组织ID        BIGINT NOT NULL,
        F关联申请单ID   BIGINT NOT NULL,
        F报销总金额     DECIMAL(18,2) NOT NULL,
        F报销事由       NVARCHAR(500) NOT NULL,
        F附件数        INT NOT NULL DEFAULT 0,
        F备注          NVARCHAR(500) NULL,
        F单据状态       INT NOT NULL DEFAULT 0,
        F凭证ID        BIGINT NULL,
        F创建时间       DATETIME2 NOT NULL DEFAULT GETDATE(),
        CONSTRAINT UQ_备用金报销编号 UNIQUE (F单据编号)
    );
END
");

            // 17. OA备用金报销单_明细
            context.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'OA备用金报销单_明细')
BEGIN
    CREATE TABLE dbo.OA备用金报销单_明细 (
        FID            BIGINT PRIMARY KEY IDENTITY(1,1),
        F报销单ID       BIGINT NOT NULL,
        F行号          INT NOT NULL,
        F费用类型       NVARCHAR(50) NOT NULL,
        F费用科目编码    NVARCHAR(30) NULL,
        F摘要          NVARCHAR(200) NOT NULL,
        F金额          DECIMAL(18,2) NOT NULL,
        F发生日期       DATE NOT NULL
    );
END
");

            // 18. OA备用金还款单
            context.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'OA备用金还款单')
BEGIN
    CREATE TABLE dbo.OA备用金还款单 (
        FID            BIGINT PRIMARY KEY IDENTITY(1,1),
        F单据编号       NVARCHAR(30) NOT NULL,
        F申请人ID       BIGINT NOT NULL,
        F部门ID        BIGINT NOT NULL,
        F组织ID        BIGINT NOT NULL,
        F关联申请单ID   BIGINT NOT NULL,
        F还款金额       DECIMAL(18,2) NOT NULL,
        F还款方式       NVARCHAR(20) NOT NULL,
        F还款说明       NVARCHAR(500) NULL,
        F单据状态       INT NOT NULL DEFAULT 0,
        F凭证ID        BIGINT NULL,
        F创建时间       DATETIME2 NOT NULL DEFAULT GETDATE(),
        CONSTRAINT UQ_备用金还款编号 UNIQUE (F单据编号)
    );
END
");

            // 19. OA备用金冲销单
            context.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'OA备用金冲销单')
BEGIN
    CREATE TABLE dbo.OA备用金冲销单 (
        FID            BIGINT PRIMARY KEY IDENTITY(1,1),
        F单据编号       NVARCHAR(30) NOT NULL,
        F申请人ID       BIGINT NOT NULL,
        F部门ID        BIGINT NOT NULL,
        F组织ID        BIGINT NOT NULL,
        F关联申请单ID   BIGINT NOT NULL,
        F备用金原额     DECIMAL(18,2) NOT NULL,
        F报销总额       DECIMAL(18,2) NOT NULL,
        F还款总额       DECIMAL(18,2) NOT NULL,
        F差额          DECIMAL(18,2) NOT NULL,
        F差额方向       NVARCHAR(20) NOT NULL,
        F备注          NVARCHAR(500) NULL,
        F单据状态       INT NOT NULL DEFAULT 0,
        F凭证ID        BIGINT NULL,
        F创建时间       DATETIME2 NOT NULL DEFAULT GETDATE(),
        CONSTRAINT UQ_备用金冲销编号 UNIQUE (F单据编号)
    );
END
");

            // 20. OA预支工资单
            context.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'OA预支工资单')
BEGIN
    CREATE TABLE dbo.OA预支工资单 (
        FID            BIGINT PRIMARY KEY IDENTITY(1,1),
        F单据编号       NVARCHAR(30) NOT NULL,
        F申请人PID       BIGINT NOT NULL,
        F部门ID        BIGINT NOT NULL,
        F组织ID        BIGINT NOT NULL,
        F预支金额       DECIMAL(18,2) NOT NULL,
        F预支月份       NVARCHAR(10) NOT NULL,
        F申请事由       NVARCHAR(500) NOT NULL,
        F收款方式       NVARCHAR(20) NOT NULL,
        F收款人名称     NVARCHAR(200) NOT NULL,
        F收款人账号     NVARCHAR(50) NULL,
        F收款人开户行   NVARCHAR(200) NULL,
        F备注          NVARCHAR(500) NULL,
        F单据状态       INT NOT NULL DEFAULT 0,
        F凭证ID        BIGINT NULL,
        F创建时间       DATETIME2 NOT NULL DEFAULT GETDATE(),
        F修改时间       DATETIME2 NULL,
        CONSTRAINT UQ_预支工资编号 UNIQUE (F单据编号)
    );
    CREATE INDEX IX_预支工资_组织 ON dbo.OA预支工资单(F组织ID, F单据状态);
END
");

            // 24. OA日程事件
            context.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'OA日程事件')
BEGIN
    CREATE TABLE dbo.OA日程事件 (
        FID              BIGINT PRIMARY KEY IDENTITY(1,1),
        F标题             NVARCHAR(200) NOT NULL,
        F描述             NVARCHAR(2000) NULL,
        F开始时间         DATETIME2 NOT NULL,
        F结束时间         DATETIME2 NOT NULL,
        F全天事件         INT NOT NULL DEFAULT 0,
        F地点             NVARCHAR(500) NULL,
        F组织者ID         BIGINT NOT NULL,
        F组织ID           BIGINT NOT NULL,
        F事件类型         INT NOT NULL DEFAULT 0,
        F状态             INT NOT NULL DEFAULT 0,
        F优先级           INT NOT NULL DEFAULT 0,
        F重复规则         NVARCHAR(500) NULL,
        F提醒设置         NVARCHAR(500) NULL,
        F钉钉事件ID       NVARCHAR(200) NULL,
        F钉钉日历ID       NVARCHAR(200) NULL,
        F创建时间         DATETIME2 NOT NULL DEFAULT GETDATE(),
        F修改时间         DATETIME2 NULL
    );
END
");

            // 25. OA日程参与者
            context.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'OA日程参与者')
BEGIN
    CREATE TABLE dbo.OA日程参与者 (
        FID              BIGINT PRIMARY KEY IDENTITY(1,1),
        F事件ID          BIGINT NOT NULL,
        F用户ID          BIGINT NOT NULL,
        F是否必需         INT NOT NULL DEFAULT 1,
        F回复状态         INT NOT NULL DEFAULT 0,
        F创建时间         DATETIME2 NOT NULL DEFAULT GETDATE()
    );
END
");

        // ===== DC文件类型种子已废除（迁移至 ExcelInputAgent 规则）=====

        // ===== 编码规则：建表 + 种子数据 =====
        context.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'SYS编码规则')
BEGIN
    CREATE TABLE [SYS编码规则] (
        [FID]        BIGINT IDENTITY(1,1) PRIMARY KEY,
        [F规则编码]   NVARCHAR(50)  NOT NULL,
        [F规则名称]   NVARCHAR(100) NOT NULL,
        [F业务实体]   NVARCHAR(100) NOT NULL,
        [F编码字段]   NVARCHAR(50)  NOT NULL DEFAULT N'F编码',
        [F前缀]      NVARCHAR(20)  NULL,
        [F日期格式]   NVARCHAR(20)  NULL,
        [F流水号长度]  INT           NOT NULL DEFAULT 4,
        [F分隔符]     NVARCHAR(5)   NULL DEFAULT N'-',
        [F重置周期]   NVARCHAR(10)  NOT NULL DEFAULT N'never',
        [F组织隔离]   BIT           NOT NULL DEFAULT 0,
        [F启用]      BIT           NOT NULL DEFAULT 1,
        [F说明]      NVARCHAR(200) NULL,
        [F创建时间]   DATETIME2     NOT NULL DEFAULT GETDATE(),
        [F修改时间]   DATETIME2     NOT NULL DEFAULT GETDATE(),
        CONSTRAINT [UQ_SYS编码规则] UNIQUE ([F规则编码])
    );
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'SYS编码序列')
BEGIN
    CREATE TABLE [SYS编码序列] (
        [FID]        BIGINT IDENTITY(1,1) PRIMARY KEY,
        [F规则ID]    BIGINT        NOT NULL,
        [F组织ID]    BIGINT        NULL,
        [F周期标识]   NVARCHAR(20)  NOT NULL DEFAULT N'',
        [F当前值]    BIGINT        NOT NULL DEFAULT 0,
        [F修改时间]   DATETIME2     NOT NULL DEFAULT GETDATE(),
        CONSTRAINT [FK_SYS编码序列_规则] FOREIGN KEY ([F规则ID]) REFERENCES [SYS编码规则]([FID]),
        CONSTRAINT [UQ_SYS编码序列] UNIQUE ([F规则ID], [F组织ID], [F周期标识])
    );
END
");

            // 种子数据：8条编码规则（幂等插入）
            context.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM [SYS编码规则] WHERE [F规则编码] = N'SUP')
    INSERT INTO [SYS编码规则] ([F规则编码],[F规则名称],[F业务实体],[F编码字段],[F前缀],[F日期格式],[F流水号长度],[F分隔符],[F重置周期],[F组织隔离])
    VALUES (N'SUP', N'供应商编码', N'SUP供应商', N'F编码', N'SUP', NULL, 4, N'-', N'never', 0);

-- EXP_BRAND / EXP_SHOP / EXP_OUTLET / EXP_AREA / EXP_BO 编码规则已全部废弃，业务侧改为手动录入编码，不再预置
-- 历史库中的残留记录由 database/96_remove_exp_code_rules.sql 清理

-- 5 种业务对象编码规则（KH/DL/YW/CB/YZ）—— BO_WD 已废弃
IF NOT EXISTS (SELECT 1 FROM [SYS编码规则] WHERE [F规则编码] = N'BO_KH')
    INSERT INTO [SYS编码规则] ([F规则编码],[F规则名称],[F业务实体],[F编码字段],[F前缀],[F日期格式],[F流水号长度],[F分隔符],[F重置周期],[F组织隔离])
    VALUES (N'BO_KH', N'客户编码', N'CRM客户', N'F编号', N'KH', NULL, 8, NULL, N'never', 0);

IF NOT EXISTS (SELECT 1 FROM [SYS编码规则] WHERE [F规则编码] = N'BO_DL')
    INSERT INTO [SYS编码规则] ([F规则编码],[F规则名称],[F业务实体],[F编码字段],[F前缀],[F日期格式],[F流水号长度],[F分隔符],[F重置周期],[F组织隔离])
    VALUES (N'BO_DL', N'代理编码', N'EXP业务代理', N'F编号', N'DL', NULL, 4, N'-', N'never', 0);

-- BO_WD 已废弃，网点编号直接使用源UID
-- IF NOT EXISTS (SELECT 1 FROM [SYS编码规则] WHERE [F规则编码] = N'BO_WD')
--     INSERT INTO [SYS编码规则] ([F规则编码],[F规则名称],[F业务实体],[F编码字段],[F前缀],[F日期格式],[F流水号长度],[F分隔符],[F重置周期],[F组织隔离])
--     VALUES (N'BO_WD', N'网点编码', N'EXP快递网点', N'F编号', N'WD', NULL, 4, N'-', N'never', 0);

IF NOT EXISTS (SELECT 1 FROM [SYS编码规则] WHERE [F规则编码] = N'BO_YW')
    INSERT INTO [SYS编码规则] ([F规则编码],[F规则名称],[F业务实体],[F编码字段],[F前缀],[F日期格式],[F流水号长度],[F分隔符],[F重置周期],[F组织隔离])
    VALUES (N'BO_YW', N'业务员编码', N'EXP业务员', N'F工号', N'YW', NULL, 4, N'-', N'never', 0);

-- BO_CB 已废弃，承包区编号由外部业务系统决定
-- IF NOT EXISTS (SELECT 1 FROM [SYS编码规则] WHERE [F规则编码] = N'BO_CB')
--     INSERT INTO [SYS编码规则] ([F规则编码],[F规则名称],[F业务实体],[F编码字段],[F前缀],[F日期格式],[F流水号长度],[F分隔符],[F重置周期],[F组织隔离])
--     VALUES (N'BO_CB', N'承包区编码', N'EXP承包区', N'F编号', N'CB', NULL, 4, N'-', N'never', 0);

IF NOT EXISTS (SELECT 1 FROM [SYS编码规则] WHERE [F规则编码] = N'BO_YZ')
    INSERT INTO [SYS编码规则] ([F规则编码],[F规则名称],[F业务实体],[F编码字段],[F前缀],[F日期格式],[F流水号长度],[F分隔符],[F重置周期],[F组织隔离])
    VALUES (N'BO_YZ', N'驿站编码', N'EXP末端驿站', N'F编号', N'YZ', NULL, 4, N'-', N'never', 0);

IF NOT EXISTS (SELECT 1 FROM [SYS编码规则] WHERE [F规则编码] = N'EXP_COST')
    INSERT INTO [SYS编码规则] ([F规则编码],[F规则名称],[F业务实体],[F编码字段],[F前缀],[F日期格式],[F流水号长度],[F分隔符],[F重置周期],[F组织隔离])
    VALUES (N'EXP_COST', N'快递成本项目编码', N'EXP成本项目', N'F编码', N'CI', NULL, 3, N'-', N'never', 0);

IF NOT EXISTS (SELECT 1 FROM [SYS编码规则] WHERE [F规则编码] = N'FIN_AUX')
    INSERT INTO [SYS编码规则] ([F规则编码],[F规则名称],[F业务实体],[F编码字段],[F前缀],[F日期格式],[F流水号长度],[F分隔符],[F重置周期],[F组织隔离])
    VALUES (N'FIN_AUX', N'辅助核算编码', N'FIN辅助核算项目', N'F编码', N'AUX', NULL, 4, N'-', N'never', 0);

IF NOT EXISTS (SELECT 1 FROM [SYS编码规则] WHERE [F规则编码] = N'FIN_AUX_PJ')
    INSERT INTO [SYS编码规则] ([F规则编码],[F规则名称],[F业务实体],[F编码字段],[F前缀],[F日期格式],[F流水号长度],[F分隔符],[F重置周期],[F组织隔离])
    VALUES (N'FIN_AUX_PJ', N'项目辅助核算编码', N'FIN辅助核算项目', N'F编码', N'PJ', NULL, 4, N'', N'never', 0);

IF NOT EXISTS (SELECT 1 FROM [SYS编码规则] WHERE [F规则编码] = N'FIN_AUX_BU')
    INSERT INTO [SYS编码规则] ([F规则编码],[F规则名称],[F业务实体],[F编码字段],[F前缀],[F日期格式],[F流水号长度],[F分隔符],[F重置周期],[F组织隔离])
    VALUES (N'FIN_AUX_BU', N'经营单元辅助核算编码', N'FIN辅助核算项目', N'F编码', N'BU', NULL, 4, N'', N'never', 0);

IF NOT EXISTS (SELECT 1 FROM [SYS编码规则] WHERE [F规则编码] = N'RL')
    INSERT INTO [SYS编码规则] ([F规则编码],[F规则名称],[F业务实体],[F编码字段],[F前缀],[F日期格式],[F流水号长度],[F分隔符],[F重置周期],[F组织隔离])
    VALUES (N'RL', N'角色编码', N'SYS角色', N'F编码', N'RL', NULL, 4, N'', N'never', 0);
");
    }

    private static void MigrateV2(STOTOPDbContext ctx)
    {
        DropBpmTables(ctx);
    }

    private static void MigrateV3(STOTOPDbContext ctx)
    {
        DropOaTables(ctx);
        PurgeRetiredReferenceData(ctx);
    }

    public static void PurgeRetiredReferenceData(STOTOPDbContext context)
    {
        if (!SeederHelper.IsSqlServer(context)) return;

        context.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'[dbo].[SYS功能权限]', N'U') IS NOT NULL
BEGIN
    IF OBJECT_ID(N'[dbo].[SYS角色权限]', N'U') IS NOT NULL
    BEGIN
        DELETE rp
          FROM [dbo].[SYS角色权限] rp
          JOIN [dbo].[SYS功能权限] p ON p.[FID] = rp.[F权限ID]
         WHERE p.[F编码] LIKE N'oa:%'
            OR p.[F路由] LIKE N'/oa%'
            OR p.[F组件路径] LIKE N'oa/%';
    END

    DELETE FROM [dbo].[SYS功能权限]
     WHERE [F编码] LIKE N'oa:%'
        OR [F路由] LIKE N'/oa%'
        OR [F组件路径] LIKE N'oa/%';
END
");
    }

    private static void DropBpmTables(STOTOPDbContext context)
    {
        if (!SeederHelper.IsSqlServer(context)) return;

        context.Database.ExecuteSqlRaw(@"
DROP TABLE IF EXISTS [OA会话消息];
DROP TABLE IF EXISTS [OA会话会话];
DROP TABLE IF EXISTS [OA流程表单字段];
DROP TABLE IF EXISTS [OA凭证生成记录];
DROP TABLE IF EXISTS [BPM流程配置版本];
DROP TABLE IF EXISTS [BPM流程日志];
DROP TABLE IF EXISTS [BPM钉钉通知记录];
DROP TABLE IF EXISTS [BPM抄送记录];
DROP TABLE IF EXISTS [BPM用户收藏流程];
DROP TABLE IF EXISTS [BPM流程发起权限];
DROP TABLE IF EXISTS [BPM流程附件];
DROP TABLE IF EXISTS [BPM审批任务];
DROP TABLE IF EXISTS [BPM审批节点配置];
DROP TABLE IF EXISTS [BPM流程实例];
DROP TABLE IF EXISTS [BPM流程配置];
DROP TABLE IF EXISTS [OA流程配置版本];
DROP TABLE IF EXISTS [OA流程日志];
DROP TABLE IF EXISTS [OA钉钉通知记录];
DROP TABLE IF EXISTS [OA抄送记录];
DROP TABLE IF EXISTS [OA用户收藏流程];
DROP TABLE IF EXISTS [OA流程发起权限];
DROP TABLE IF EXISTS [OA流程附件];
DROP TABLE IF EXISTS [OA审批任务];
DROP TABLE IF EXISTS [OA审批节点配置];
DROP TABLE IF EXISTS [OA流程实例];
DROP TABLE IF EXISTS [OA流程配置];
");
    }

    private static void DropOaTables(STOTOPDbContext context)
    {
        if (!SeederHelper.IsSqlServer(context)) return;

        context.Database.ExecuteSqlRaw(@"
DECLARE @retiredOaTables TABLE ([name] SYSNAME PRIMARY KEY);
INSERT INTO @retiredOaTables ([name])
VALUES
    (N'OA会话消息'),
    (N'OA会话会话'),
    (N'OA对话消息'),
    (N'OA对话会话'),
    (N'OA流程表单字段'),
    (N'OA凭证生成记录'),
    (N'BPM流程配置版本'),
    (N'BPM流程日志'),
    (N'BPM钉钉通知记录'),
    (N'BPM抄送记录'),
    (N'BPM用户收藏流程'),
    (N'BPM流程发起权限'),
    (N'BPM流程附件'),
    (N'BPM审批任务'),
    (N'BPM审批节点配置'),
    (N'BPM流程实例'),
    (N'BPM流程配置'),
    (N'OA流程配置版本'),
    (N'OA流程日志'),
    (N'OA钉钉通知记录'),
    (N'OA抄送记录'),
    (N'OA用户收藏流程'),
    (N'OA流程发起权限'),
    (N'OA流程附件'),
    (N'OA审批任务'),
    (N'OA审批节点配置'),
    (N'OA流程实例'),
    (N'OA流程配置'),
    (N'OA日程参与者'),
    (N'OA日程事件'),
    (N'OA审批委托'),
    (N'OA费用类型科目映射'),
    (N'OA费用类型'),
    (N'OA对外付款单_明细'),
    (N'OA对外付款单'),
    (N'OA备用金报销单_明细'),
    (N'OA备用金报销单'),
    (N'OA备用金还款单'),
    (N'OA备用金冲销单'),
    (N'OA备用金申请单'),
    (N'OA费用报销单_明细'),
    (N'OA费用报销单'),
    (N'OA费用请款单'),
    (N'OA借款申请单'),
    (N'OA预支工资单');

DECLARE @dropOaFkSql NVARCHAR(MAX) = N'';
SELECT @dropOaFkSql +=
    N'ALTER TABLE '
    + QUOTENAME(OBJECT_SCHEMA_NAME(fk.parent_object_id))
    + N'.'
    + QUOTENAME(OBJECT_NAME(fk.parent_object_id))
    + N' DROP CONSTRAINT '
    + QUOTENAME(fk.name)
    + N';' + CHAR(13) + CHAR(10)
FROM sys.foreign_keys fk
WHERE OBJECT_NAME(fk.parent_object_id) IN (SELECT [name] FROM @retiredOaTables)
   OR OBJECT_NAME(fk.referenced_object_id) IN (SELECT [name] FROM @retiredOaTables);

IF LEN(@dropOaFkSql) > 0
    EXEC sp_executesql @dropOaFkSql;

DROP TABLE IF EXISTS [OA会话消息];
DROP TABLE IF EXISTS [OA会话会话];
DROP TABLE IF EXISTS [OA对话消息];
DROP TABLE IF EXISTS [OA对话会话];
DROP TABLE IF EXISTS [OA流程表单字段];
DROP TABLE IF EXISTS [OA凭证生成记录];
DROP TABLE IF EXISTS [BPM流程配置版本];
DROP TABLE IF EXISTS [BPM流程日志];
DROP TABLE IF EXISTS [BPM钉钉通知记录];
DROP TABLE IF EXISTS [BPM抄送记录];
DROP TABLE IF EXISTS [BPM用户收藏流程];
DROP TABLE IF EXISTS [BPM流程发起权限];
DROP TABLE IF EXISTS [BPM流程附件];
DROP TABLE IF EXISTS [BPM审批任务];
DROP TABLE IF EXISTS [BPM审批节点配置];
DROP TABLE IF EXISTS [BPM流程实例];
DROP TABLE IF EXISTS [BPM流程配置];
DROP TABLE IF EXISTS [OA流程配置版本];
DROP TABLE IF EXISTS [OA流程日志];
DROP TABLE IF EXISTS [OA钉钉通知记录];
DROP TABLE IF EXISTS [OA抄送记录];
DROP TABLE IF EXISTS [OA用户收藏流程];
DROP TABLE IF EXISTS [OA流程发起权限];
DROP TABLE IF EXISTS [OA流程附件];
DROP TABLE IF EXISTS [OA审批任务];
DROP TABLE IF EXISTS [OA审批节点配置];
DROP TABLE IF EXISTS [OA流程实例];
DROP TABLE IF EXISTS [OA流程配置];
DROP TABLE IF EXISTS [OA日程参与者];
DROP TABLE IF EXISTS [OA日程事件];
DROP TABLE IF EXISTS [OA审批委托];
DROP TABLE IF EXISTS [OA费用类型科目映射];
DROP TABLE IF EXISTS [OA费用类型];
DROP TABLE IF EXISTS [OA对外付款单_明细];
DROP TABLE IF EXISTS [OA对外付款单];
DROP TABLE IF EXISTS [OA备用金报销单_明细];
DROP TABLE IF EXISTS [OA备用金报销单];
DROP TABLE IF EXISTS [OA备用金还款单];
DROP TABLE IF EXISTS [OA备用金冲销单];
DROP TABLE IF EXISTS [OA备用金申请单];
DROP TABLE IF EXISTS [OA费用报销单_明细];
DROP TABLE IF EXISTS [OA费用报销单];
DROP TABLE IF EXISTS [OA费用请款单];
DROP TABLE IF EXISTS [OA借款申请单];
DROP TABLE IF EXISTS [OA预支工资单];
");
    }
}
