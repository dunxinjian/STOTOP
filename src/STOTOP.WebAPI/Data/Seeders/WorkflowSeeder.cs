using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;

namespace STOTOP.WebAPI.Data.Seeders;

/// <summary>
/// WF（Workflow）模块 Seeder —— 触发动作种子数据
/// </summary>
public static class WorkflowSeeder
{
    private const string Module = "Workflow";

    /// <summary>
    /// 版本化迁移入口 - WF模块
    /// </summary>
    public static void Migrate(STOTOPDbContext ctx)
    {
        MigrationRunner.RunMigrations(ctx, Module, new List<MigrationStep>
        {
            new(1, "WF触发动作种子数据", MigrateV1),
            new(2, "新增 cardflow.apply 发起审批入口 (2026-06-16)", MigrateV2),
            new(3, "阶段0多租户: Workflow 6张租户表加 F租户ID 隔离键列(NOT NULL DEFAULT 0,不启用过滤器)+租户索引 (2026-07-01)", MigrateV3),
            new(4, "阶段0多租户: Workflow 存量行 F租户ID 回填到根组织单租户(=根组织id) (2026-07-01)", MigrateV4),
        });
    }

    /// <summary>阶段0 多租户隔离：需加 F租户ID 的 6 张租户表（全覆盖，见 design/24-tenant-migration-playbook.md）。</summary>
    private static readonly string[] Phase0TenantTables =
    {
        "WF工作项", "WF派发规则", "WF撤销日志", "WF问题包", "WF链路评论", "WF触发动作",
    };

    /// <summary>
    /// 阶段0·加列+索引：给租户表加 F租户ID 隔离键列 + 租户索引。仅 DDL、幂等(IF NOT EXISTS)。
    /// 列定义 = bigint NOT NULL DEFAULT 0，与模型(long FTenantId + HasDefaultValue(0L))经 SchemaAutoSync 在 dev 自动生成的列一致；
    /// prod 不跑 SchemaAutoSync，靠本步显式 ALTER 落列，避免 dev/prod 漂移。存量行先得 0(=未分配租户哨兵)，回填见 V4。
    /// </summary>
    private static void MigrateV3(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        foreach (var t in Phase0TenantTables)
        {
            SeederHelper.ExecuteRawSql(ctx, $@"
            IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = N'{t}' AND COLUMN_NAME = N'F租户ID')
            ALTER TABLE [{t}] ADD [F租户ID] bigint NOT NULL CONSTRAINT [DF_{t}_F租户ID] DEFAULT 0;");
        }

        foreach (var t in Phase0TenantTables)
        {
            SeederHelper.ExecuteRawSql(ctx, $@"
            IF NOT EXISTS (SELECT * FROM sys.indexes
                WHERE name = N'IX_{t}_租户ID' AND object_id = OBJECT_ID(N'{t}'))
            CREATE INDEX [IX_{t}_租户ID] ON [{t}] ([F租户ID]);");
        }
    }

    /// <summary>
    /// 阶段0·回填：当前生产库整棵组织树属单客户=单租户(见 design/23 v2)，存量行 F租户ID 全归根组织节点 FID。
    /// 仅回填 WHERE F租户ID=0(未分配行)，幂等；fresh 库无存量业务行 → no-op，绝不误填别的租户。
    /// </summary>
    private static void MigrateV4(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        foreach (var t in Phase0TenantTables)
        {
            SeederHelper.ExecuteRawSql(ctx, $@"
            DECLARE @tenant bigint = (SELECT TOP 1 [FID] FROM [SYS组织架构] WHERE [F父ID] = 0 ORDER BY [FID]);
            IF @tenant IS NOT NULL
                UPDATE [{t}] SET [F租户ID] = @tenant WHERE [F租户ID] = 0;");
        }
    }

    private static void MigrateV1(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // ===== WF触发动作种子数据 =====
        ctx.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM [WF触发动作])
BEGIN
    INSERT INTO [WF触发动作] ([F组织ID], [F标识], [F名称], [F图标], [F模块], [F路由], [F类别], [F权限码], [F排序], [F描述]) VALUES
    (0, 'datacenter.upload', N'上传数据', 'CloudUploadOutlined', 'datacenter', '/datacenter/upload-center', 'upload', NULL, 10, N'上传Excel数据文件进行导入处理'),
    (0, 'datacenter.import-rule', N'配置导入规则', 'SettingOutlined', 'datacenter', '/datacenter/import-rules', 'create', 'datacenter.admin', 80, N'配置数据导入的解析和校验规则'),
    (0, 'finance.voucher.create', N'录入凭证', 'FormOutlined', 'finance', '/finance/vouchers/create', 'create', NULL, 20, N'手动录入会计凭证'),
    (0, 'finance.period-close', N'发起期末结转', 'CalendarOutlined', 'finance', '/finance/period-closing', 'apply', 'finance.period', 70, N'发起会计期间的期末结转流程'),
    (0, 'express.recalc', N'发起重算', 'ReloadOutlined', 'express', '/express/billing/recalc', 'apply', 'express.billing', 60, N'对选定账单发起费用重新计算'),
    (0, 'express.dispute', N'提交账单异议', 'ExclamationCircleOutlined', 'express', '/express/billing/dispute', 'apply', NULL, 65, N'对计费结果提交异议申诉'),
    (0, 'task.create', N'新建任务', 'PlusCircleOutlined', 'task', '/workhub?action=create-task', 'create', NULL, 30, N'创建一个新的工作任务'),
    (0, 'cardflow.start', N'发起卡片流程', 'FileTextOutlined', 'cardflow', '/cardflow/upload', 'apply', NULL, 40, N'通过CardFlow发起业务流程');
END
");
    }

    private static void MigrateV2(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        ctx.Database.ExecuteSqlRaw(@"
IF NOT EXISTS (SELECT 1 FROM [WF触发动作] WHERE [F标识] = 'cardflow.apply')
    INSERT INTO [WF触发动作] ([F组织ID],[F标识],[F名称],[F图标],[F模块],[F路由],[F类别],[F权限码],[F排序],[F描述]) VALUES
    (0, 'cardflow.apply', N'发起审批', 'AuditOutlined', 'cardflow', '/cardflow/home', 'apply', NULL, 41, N'发起一条卡片审批流程（如费用报销）');
");
    }
}
