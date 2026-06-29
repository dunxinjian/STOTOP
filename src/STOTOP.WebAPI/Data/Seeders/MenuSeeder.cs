using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;

namespace STOTOP.WebAPI.Data.Seeders;

public static class MenuSeeder
{
    private const string Module = "Menu";

    public static void Migrate(STOTOPDbContext ctx)
    {
        var steps = new List<MigrationStep>
        {
            new(1, "菜单权限基线 (2026-05-26)", MigrateV1),
            new(2, "快递报价菜单名称修正 (2026-06-02)", MigrateV2),
            new(3, "快递报价→快递报价管理 (2026-06-03)", MigrateV3),
            new(4, "快递报价菜单去重+改名 (2026-06-03)", MigrateV4),
            new(5, "快递报价查询菜单名称修正 (2026-06-03)", MigrateV5),
            new(6, "新增内测反馈中心菜单 (2026-06-07)", MigrateV6),
        };
        MigrationRunner.RunMigrations(ctx, Module, steps);

        // 版本化迁移的防御性补充：每次启动无条件执行，确保菜单名称收敛到期望状态
        EnsureMenuNames(ctx);
    }

    /// <summary>
    /// V1：菜单权限基线。
    /// 所有菜单数据（SYS功能权限 441行 + SYS角色权限）已由 SystemSeeder 统一管理，
    /// 本模块不再重复插入。如需新增模块专属菜单，请在此追加新版本。
    /// </summary>
    private static void MigrateV1(STOTOPDbContext ctx)
    {
        // 菜单数据已由 SystemSeeder 的 SYS功能权限 导出统一覆盖，无需额外操作。
    }

    /// <summary>
    /// V2：将 /express/quotation 路由对应的菜单名称从"报价管理"改为"快递报价"。
    /// </summary>
    private static void MigrateV2(STOTOPDbContext ctx)
    {
        ctx.Database.ExecuteSqlRaw(@"
UPDATE [SYS功能权限]
SET [F名称] = N'快递报价'
WHERE [F路由] = N'/express/quotation'
  AND [F名称] = N'报价管理'
");
    }

    /// <summary>
    /// V3：原本尝试将“快递报价”改名，但 WHERE 条件未命中，无实际效果。
    /// </summary>
    private static void MigrateV3(STOTOPDbContext ctx)
    {
        // 已由 V4 替代
    }

    /// <summary>
    /// V4：将 /express/quotation 路由仅保留一条菜单并命名为"快递报价管理"，删除多余记录。
    /// </summary>
    private static void MigrateV4(STOTOPDbContext ctx)
    {
        ctx.Database.ExecuteSqlRaw(@"
-- 将第一条记录改名为快递报价管理
UPDATE TOP(1) [SYS功能权限]
SET [F名称] = N'快递报价管理'
WHERE [F路由] = N'/express/quotation'
  AND [F名称] <> N'快递报价管理';

-- 删除同路由下多余的菜单记录
DELETE FROM [SYS功能权限]
WHERE [F路由] = N'/express/quotation'
  AND [F名称] <> N'快递报价管理';
");
    }

    /// <summary>
    /// V5：将 /express/quotation-workbench 路由对应的菜单名称改为"快递报价查询"。
    /// </summary>
    private static void MigrateV5(STOTOPDbContext ctx)
    {
        ctx.Database.ExecuteSqlRaw(@"
UPDATE [SYS功能权限]
SET [F名称] = N'快递报价查询'
WHERE [F路由] = N'/express/quotation-workbench'
  AND [F名称] <> N'快递报价查询'");
    }

    /// <summary>
    /// V6：新增系统设置下的内测反馈中心菜单和反馈管理权限。
    /// </summary>
    private static void MigrateV6(STOTOPDbContext ctx)
    {
        ctx.Database.ExecuteSqlRaw(@"
DECLARE @SystemModuleId BIGINT;
SELECT @SystemModuleId = [FID]
FROM [SYS功能权限]
WHERE [F编码] = N'sys:manage' AND [F类型] = N'模块';

IF @SystemModuleId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [SYS功能权限] WHERE [F编码] = N'sys:feedback')
BEGIN
    INSERT INTO [SYS功能权限] ([F名称],[F编码],[F类型],[F父ID],[F路由],[F组件路径],[F图标],[F排序],[F状态],[F是否可见])
    VALUES (N'反馈中心',N'sys:feedback',N'菜单',@SystemModuleId,N'/system/feedback',N'system/FeedbackCenter',N'MessageOutlined',95,1,1);
END

DECLARE @FeedbackMenuId BIGINT;
SELECT @FeedbackMenuId = [FID] FROM [SYS功能权限] WHERE [F编码] = N'sys:feedback';

IF @FeedbackMenuId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [SYS功能权限] WHERE [F编码] = N'sys:feedback:manage')
BEGIN
    INSERT INTO [SYS功能权限] ([F名称],[F编码],[F类型],[F父ID],[F排序],[F状态],[F是否可见])
    VALUES (N'管理反馈',N'sys:feedback:manage',N'按钮',@FeedbackMenuId,1,1,0);
END

INSERT INTO [SYS角色权限] ([F角色ID], [F权限ID])
SELECT 1, p.FID
FROM [SYS功能权限] p
WHERE p.[F编码] IN (N'sys:feedback', N'sys:feedback:manage')
AND NOT EXISTS (
    SELECT 1 FROM [SYS角色权限]
    WHERE [F角色ID] = 1 AND [F权限ID] = p.FID
);
");
    }

    /// <summary>
    /// 声明式菜单名称注册表。每次启动无条件执行，确保菜单名称收敛到期望状态。
    /// 这是对版本化迁移的防御性补充——无论历史版本是否正确执行，名称都会被强制修正。
    /// 新的菜单改名只需在 NameRegistry 中添加一行，无需新增版本号。
    /// </summary>
    private static void EnsureMenuNames(STOTOPDbContext ctx)
    {
        var nameRegistry = new Dictionary<string, string>
        {
            ["/express/quotation"] = "快递报价管理",
            ["/express/quotation-workbench"] = "快递报价查询",
            ["/system/feedback"] = "反馈中心",
        };

        foreach (var (route, desiredName) in nameRegistry)
        {
            // 1. 将匹配路由的第一条记录改为期望名称
            ctx.Database.ExecuteSqlRaw(
                "UPDATE TOP(1) [SYS功能权限] SET [F名称] = {0} WHERE [F路由] = {1} AND [F名称] <> {0}",
                desiredName, route);

            // 2. 删除同路由下的重复记录（只保留期望名称的那条）
            ctx.Database.ExecuteSqlRaw(
                "DELETE FROM [SYS功能权限] WHERE [F路由] = {0} AND [F名称] <> {1}",
                route, desiredName);
        }
    }
}
