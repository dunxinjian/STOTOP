using STOTOP.Infrastructure.Data;

namespace STOTOP.WebAPI.Data.Seeders;

/// <summary>
/// Quality（质量/网点质控归一）模块版本化迁移。
/// 表结构由 EF Core + CreateMissingTables + SchemaAutoSync 建立；本 Seeder 负责多租户阶段0 的
/// “加 F租户ID 列 + 回填”（照 FinanceSeeder V12/V13 同款：NOT NULL DEFAULT 0 + 租户索引 + 回填根组织单租户）。
/// 注：CarrierQualitySeeder(Module="CarrierQuality") 只播网点质控看板菜单，不负责 QL* 业务表——故本模块另起
/// 独立 QualitySeeder(Module="Quality")，与其它模块隔离键列迁移保持一致、可发现。
/// 非 critical 业务模块：迁移失败仅告警、不阻启动（见 DatabaseSeederAdapter.businessModules）。
/// </summary>
public static class QualitySeeder
{
    private const string Module = "Quality";

    /// <summary>阶段0 多租户隔离：需加 F租户ID 的 9 张租户表（全覆盖，见 design/24-tenant-migration-playbook.md）。</summary>
    private static readonly string[] Phase0TenantTables =
    {
        "QL复盘记录", "QL异常单", "QL预警配置", "QL绩效记录", "QL检测规则",
        "QL申通_网点日质量指标", "QL申通_质量问题字典", "QL申通_员工日质量指标", "QL申通_承运商质量事件",
    };

    public static void Migrate(STOTOPDbContext ctx)
    {
        MigrationRunner.RunMigrations(ctx, Module, new List<MigrationStep>
        {
            new(1, "阶段0多租户: Quality 9张租户表加 F租户ID 隔离键列(NOT NULL DEFAULT 0,不启用过滤器)+租户索引 (2026-07-01)", MigrateV1),
            new(2, "阶段0多租户: Quality 存量行 F租户ID 回填到根组织单租户(=根组织id) (2026-07-01)", MigrateV2),
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
}
