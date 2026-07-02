using STOTOP.Infrastructure.Data;

namespace STOTOP.WebAPI.Data.Seeders;

/// <summary>
/// HR 模块版本化迁移。HR 此前无任何租户隔离实体；用户裁定员工按租户隔离（区域公司=用工主体），
/// 故新起 HrSeeder(Module="HR") 给 HR员工 加 F租户ID 列+索引+回填（照其它模块阶段0/1 同款：
/// NOT NULL DEFAULT 0 + 租户索引 + 回填根组织单租户）。
/// 表结构由 CreateMissingTables + SchemaAutoSync 建（HR员工 非 STG，自动建表）；prod 不跑 SchemaAutoSync，
/// 靠本 seeder 显式 ALTER 落列，避免 dev/prod 漂移。非 critical 业务模块：迁移失败仅告警、不阻启动。
/// </summary>
public static class HrSeeder
{
    private const string Module = "HR";

    public static void Migrate(STOTOPDbContext ctx)
    {
        MigrationRunner.RunMigrations(ctx, Module, new List<MigrationStep>
        {
            new(1, "阶段1收尾(裁定): HR员工(员工按租户隔离) 加 F租户ID 列(NOT NULL DEFAULT 0)+索引+回填根组织单租户 (2026-07-02)", MigrateV1),
        });
    }

    private static void MigrateV1(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;

        SeederHelper.ExecuteRawSql(ctx, @"
        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_NAME = N'HR员工' AND COLUMN_NAME = N'F租户ID')
        ALTER TABLE [HR员工] ADD [F租户ID] bigint NOT NULL CONSTRAINT [DF_HR员工_F租户ID] DEFAULT 0;");

        SeederHelper.ExecuteRawSql(ctx, @"
        IF NOT EXISTS (SELECT * FROM sys.indexes
            WHERE name = N'IX_HR员工_租户ID' AND object_id = OBJECT_ID(N'HR员工'))
        CREATE INDEX [IX_HR员工_租户ID] ON [HR员工] ([F租户ID]);");

        SeederHelper.ExecuteRawSql(ctx, @"
        DECLARE @tenant bigint = (SELECT TOP 1 [FID] FROM [SYS组织架构] WHERE [F父ID] = 0 ORDER BY [FID]);
        IF @tenant IS NOT NULL
            UPDATE [HR员工] SET [F租户ID] = @tenant WHERE [F租户ID] = 0;");
    }
}
