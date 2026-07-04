using STOTOP.Infrastructure.Data;

namespace STOTOP.WebAPI.Data.Seeders;

/// <summary>
/// Insurance（保险/共保基金）模块版本化迁移。
/// 表结构由 EF Core + CreateMissingTables + SchemaAutoSync 建立；本 Seeder 负责多租户阶段0 的
/// “加 F租户ID 列 + 回填”（照 FinanceSeeder V12/V13 同款：NOT NULL DEFAULT 0 + 租户索引 + 回填根组织单租户）。
/// 非 critical 业务模块：迁移失败仅告警、不阻启动（见 DatabaseSeederAdapter.businessModules）。
/// </summary>
public static class InsuranceSeeder
{
    private const string Module = "Insurance";

    /// <summary>阶段0 多租户隔离：需加 F租户ID 的 8 张租户表（全覆盖，见 design/24-tenant-migration-playbook.md）。</summary>
    private static readonly string[] Phase0TenantTables =
    {
        "INS出险记录", "INS理赔记录", "INS共保基金缴费", "INS保险公司",
        "INS理赔审批配置", "INS理赔审批记录", "INS共保基金", "INS保单",
    };

    public static void Migrate(STOTOPDbContext ctx)
    {
        MigrationRunner.RunMigrations(ctx, Module, new List<MigrationStep>
        {
            new(1, "阶段0多租户: Insurance 8张租户表加 F租户ID 隔离键列(NOT NULL DEFAULT 0,不启用过滤器)+租户索引 (2026-07-01)", MigrateV1),
            new(2, "阶段0多租户: Insurance 存量行 F租户ID 回填到根组织单租户(=根组织id) (2026-07-01)", MigrateV2),
            new(3, "阶段0收尾: Insurance 20个非租户列(审计时间戳/共保基金金额/理赔可驳回) 硬化为 NOT NULL(存量0 NULL行,无索引依赖) (2026-07-04)", MigrateV3),
        });
    }

    /// <summary>
    /// 阶段0·加列+索引：给租户表加 F租户ID 隔离键列 + 租户索引。仅 DDL、幂等(IF NOT EXISTS)。
    /// 列定义 = bigint NOT NULL DEFAULT 0，与模型(long FTenantId + HasDefaultValue(0L))经 SchemaAutoSync 在 dev 自动生成的列一致；
    /// prod 不跑 SchemaAutoSync，靠本步显式 ALTER 落列，避免 dev/prod 漂移。存量行先得 0(=未分配租户哨兵)，回填见 V2。
    /// </summary>
    private static void MigrateV1(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        // ① 加列（幂等）。
        foreach (var t in Phase0TenantTables)
        {
            SeederHelper.ExecuteRawSql(ctx, $@"
            IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = N'{t}' AND COLUMN_NAME = N'F租户ID')
            ALTER TABLE [{t}] ADD [F租户ID] bigint NOT NULL CONSTRAINT [DF_{t}_F租户ID] DEFAULT 0;");
        }

        // ② 建租户索引（幂等、独立 batch —— 与①分开，避免同批 ALTER ADD 后引用新列的延迟名称解析失败）。
        //    索引名与各 Configuration 的 HasDatabaseName("IX_{表}_租户ID") 一致，与 CreateRelationalArtifacts 互不重复。
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
    private static void MigrateV2(STOTOPDbContext ctx)
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
    /// 阶段0收尾·非租户列硬化目标：这些列建表早于模型标 NOT NULL，库中仍可空，与模型不一致；
    /// SchemaAutoSync 对可空性只提示不自动改。均为审计时间戳 / 共保基金金额 / 理赔审批布尔等非隔离业务列，
    /// (2026-07-04 核 47.105.65.51/stotop：这些列存量 NULL 行均为 0，且无索引依赖 → 直接 ALTER，无需回填/落索引。)
    /// 不加 DEFAULT：与模型一致，新行由实体/审计管道回填。(Table, Col, ALTER-ready 类型)。
    /// </summary>
    private static readonly (string Table, string Col, string Type)[] Phase0NotNullHarden =
    {
        ("INS保单", "F创建时间", "datetime2"), ("INS保单", "F更新时间", "datetime2"),
        ("INS保险公司", "F创建时间", "datetime2"), ("INS保险公司", "F更新时间", "datetime2"),
        ("INS出险记录", "F创建时间", "datetime2"), ("INS出险记录", "F更新时间", "datetime2"),
        ("INS共保基金", "F免赔额", "decimal(18,2)"), ("INS共保基金", "F创建时间", "datetime2"),
        ("INS共保基金", "F基金余额", "decimal(18,2)"), ("INS共保基金", "F更新时间", "datetime2"),
        ("INS共保基金", "F累计缴费", "decimal(18,2)"), ("INS共保基金", "F累计赔付", "decimal(18,2)"),
        ("INS共保基金缴费", "F创建时间", "datetime2"), ("INS共保基金缴费", "F更新时间", "datetime2"),
        ("INS理赔记录", "F创建时间", "datetime2"), ("INS理赔记录", "F更新时间", "datetime2"),
        ("INS理赔审批记录", "F创建时间", "datetime2"),
        ("INS理赔审批配置", "F可驳回", "bit"), ("INS理赔审批配置", "F创建时间", "datetime2"),
        ("INS理赔审批配置", "F更新时间", "datetime2"),
    };

    /// <summary>
    /// 阶段0收尾·硬化为 NOT NULL：幂等——仅在"列当前可空 且 已无 NULL"时 ALTER，硬化后再跑为 no-op；
    /// fresh 库(EF 按模型直接建 NOT NULL)亦 no-op。这些列均无索引依赖，无需 drop/recreate。
    /// </summary>
    private static void MigrateV3(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        foreach (var (t, c, type) in Phase0NotNullHarden)
        {
            SeederHelper.ExecuteRawSql(ctx, $@"
            IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                       WHERE TABLE_NAME = N'{t}' AND COLUMN_NAME = N'{c}' AND IS_NULLABLE = 'YES')
               AND NOT EXISTS (SELECT 1 FROM [{t}] WHERE [{c}] IS NULL)
                ALTER TABLE [{t}] ALTER COLUMN [{c}] {type} NOT NULL;");
        }
    }
}
