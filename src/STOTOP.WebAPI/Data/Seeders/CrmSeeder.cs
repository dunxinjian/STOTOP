using STOTOP.Infrastructure.Data;

namespace STOTOP.WebAPI.Data.Seeders;

/// <summary>
/// CRM 模块版本化迁移。
/// 表结构由 EF Core + SchemaAutoSync 补齐，本 Seeder 只处理需要显式下线的历史列。
/// </summary>
public static class CrmSeeder
{
    private const string Module = "CRM";

    public static void Migrate(STOTOPDbContext ctx)
    {
        MigrationRunner.RunMigrations(ctx, Module, new List<MigrationStep>
        {
            new(1, "下线CRM客户历史冗余字段 (2026-06-11)", MigrateV1),
            new(2, "阶段0多租户: CRM 18张租户表加 F租户ID 隔离键列(NOT NULL DEFAULT 0,不启用过滤器)+租户索引 (2026-07-01)", MigrateV2),
            new(3, "阶段0多租户: CRM 存量行 F租户ID 回填到根组织单租户(=根组织id) (2026-07-01)", MigrateV3),
            new(4, "阶段0收尾: CRM 6张表 F组织ID 硬化为 NOT NULL(存量0 NULL行,回填仅作安全网) (2026-07-03)", MigrateV4),
        });
    }

    /// <summary>阶段0 多租户隔离：需加 F租户ID 的 18 张租户表（全覆盖，见 design/24-tenant-migration-playbook.md）。</summary>
    private static readonly string[] Phase0TenantTables =
    {
        "CRM奖金明细", "CRM奖金方案", "CRM返佣申请", "CRM客户账户", "CRM客户",
        "CRM客户联系人", "CRM客户毛利", "CRM客户流转记录", "CRM外部联系人", "CRM预付款记录",
        "CRM推荐记录", "CRM角色映射", "CRM服务反馈", "CRM服务工单", "CRM工单处理记录",
        "CRM拜访记录", "CRM运单号发放", "CRM号段池",
    };

    /// <summary>
    /// 阶段0·加列+索引：给租户表加 F租户ID 隔离键列 + 租户索引。仅 DDL、幂等(IF NOT EXISTS)。
    /// 列定义 = bigint NOT NULL DEFAULT 0，与模型(long FTenantId + HasDefaultValue(0L))经 SchemaAutoSync 在 dev 自动生成的列一致；
    /// prod 不跑 SchemaAutoSync，靠本步显式 ALTER 落列，避免 dev/prod 漂移。存量行先得 0(=未分配租户哨兵)，回填见 V3。
    /// </summary>
    private static void MigrateV2(STOTOPDbContext ctx)
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
    private static void MigrateV3(STOTOPDbContext ctx)
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

    /// <summary>
    /// 阶段0收尾·F组织ID 硬化目标：这 6 张表的 F组织ID 建表早于 FOrgId 标 NOT NULL，库中仍可空，
    /// 与模型(IOrgScoped 的 long FOrgId)不一致，SchemaAutoSync 对可空性只提示不自动改。
    /// 其余 12 张租户表的 F组织ID 已是 NOT NULL，不在此列。
    /// (2026-07-03 核 47.105.65.51/stotop：这 6 表 F组织ID 存量 NULL 行均为 0，故回填仅作安全网。)
    /// </summary>
    private static readonly string[] Phase0OrgHardenTables =
    {
        "CRM客户", "CRM客户毛利", "CRM奖金方案", "CRM推荐记录", "CRM预付款记录", "CRM服务反馈",
    };

    /// <summary>
    /// 阶段0收尾·F组织ID 硬化：先把存量 NULL 行回填到根组织(单租户根节点，与 V3 的租户回填同源)，
    /// 再 ALTER 为 NOT NULL。不加 DEFAULT：模型 FOrgId 无 HasDefaultValue，新行由 IOrgScoped 保存时
    /// 自动回填组织，0 非合法组织。
    /// 这 6 张表各有单列非唯一索引 IX_&lt;表&gt;_F组织ID 依赖该列，SQL Server 不允许直接 ALTER(err 5074)，
    /// 故须"落索引 → 改列 → 建回索引"三步(与模型 HasIndex(FOrgId) 的定义一致，无过滤/非唯一)。
    /// 幂等：回填仅命中 IS NULL；整段仅在"列当前可空 且 已无 NULL"时执行，硬化后再跑为 no-op。
    /// </summary>
    private static void MigrateV4(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        foreach (var t in Phase0OrgHardenTables)
        {
            var idx = $"IX_{t}_F组织ID";
            SeederHelper.ExecuteRawSql(ctx, $@"
            DECLARE @root bigint = (SELECT TOP 1 [FID] FROM [SYS组织架构] WHERE [F父ID] = 0 ORDER BY [FID]);
            IF @root IS NOT NULL
                UPDATE [{t}] SET [F组织ID] = @root WHERE [F组织ID] IS NULL;

            IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                       WHERE TABLE_NAME = N'{t}' AND COLUMN_NAME = N'F组织ID' AND IS_NULLABLE = 'YES')
               AND NOT EXISTS (SELECT 1 FROM [{t}] WHERE [F组织ID] IS NULL)
            BEGIN
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'{idx}' AND object_id = OBJECT_ID(N'{t}'))
                    DROP INDEX [{idx}] ON [{t}];

                ALTER TABLE [{t}] ALTER COLUMN [F组织ID] bigint NOT NULL;

                CREATE INDEX [{idx}] ON [{t}] ([F组织ID]);
            END");
        }
    }

    private static void MigrateV1(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        SeederHelper.DropIndexSafe(ctx, "CRM客户", "IX_CRM客户_客户编号");
        SeederHelper.DropColumnSafe(ctx, "CRM客户", "F客户编号");
        SeederHelper.DropColumnSafe(ctx, "CRM客户", "F业务员名称原值");
        SeederHelper.DropColumnSafe(ctx, "CRM客户", "F源UID");
    }
}
