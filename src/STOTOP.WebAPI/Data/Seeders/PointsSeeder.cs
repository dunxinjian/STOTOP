using STOTOP.Infrastructure.Data;

namespace STOTOP.WebAPI.Data.Seeders;

/// <summary>
/// 积分管理模块（Points）版本化迁移
/// V1：基线占位 — 建表与列同步由 SchemaAutoSync 完成，本模块无预置种子数据。
/// </summary>
public static class PointsSeeder
{
    private const string Module = "Points";

    public static void Migrate(STOTOPDbContext ctx)
    {
        var steps = new List<MigrationStep>
        {
            new(1, "积分模块基线（建表交由 Schema Auto-Sync 完成）", MigrateV1),
            new(2, "阶段0多租户: Points 10张租户表加 F租户ID 隔离键列(NOT NULL DEFAULT 0,不启用过滤器)+租户索引 (2026-07-01)", MigrateV2),
            new(3, "阶段0多租户: Points 存量行 F租户ID 回填到根组织单租户(=根组织id) (2026-07-01)", MigrateV3),
        };
        MigrationRunner.RunMigrations(ctx, Module, steps);
    }

    /// <summary>阶段0 多租户隔离：需加 F租户ID 的 10 张租户表（全覆盖，见 design/24-tenant-migration-playbook.md）。</summary>
    private static readonly string[] Phase0TenantTables =
    {
        "PM管理层奖扣任务", "PM积分记录", "PM积分规则", "PM积分排名快照", "PM积分来源",
        "PM积分账户", "PM积分清算记录", "PM兑换记录", "PM兑换商品", "PM积分申请",
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

    private static void MigrateV1(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;
        // 占位：无需执行 DDL/DML，建表逻辑由 CreateMissingTables + SchemaAutoSync 完成。
    }
}
