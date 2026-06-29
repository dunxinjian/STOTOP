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
        });
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
