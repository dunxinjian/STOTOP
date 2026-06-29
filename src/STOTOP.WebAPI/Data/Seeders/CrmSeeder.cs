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
        });
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
