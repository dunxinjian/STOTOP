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
        };
        MigrationRunner.RunMigrations(ctx, Module, steps);
    }

    private static void MigrateV1(STOTOPDbContext ctx)
    {
        if (!SeederHelper.IsSqlServer(ctx)) return;
        // 占位：无需执行 DDL/DML，建表逻辑由 CreateMissingTables + SchemaAutoSync 完成。
    }
}
