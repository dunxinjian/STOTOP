using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.System.Entities;

namespace STOTOP.WebAPI.Data.Seeders;

/// <summary>
/// 基础数据种子（组织类型、辅助核算类型等）
/// </summary>
public static class BasicDataSeeder
{
    private const string Module = "BasicData";

    public static void Migrate(STOTOPDbContext ctx)
    {
        var steps = new List<MigrationStep>
        {
            new(1, "基础数据初始化", MigrateV1),
        };
        MigrationRunner.RunMigrations(ctx, Module, steps);
    }

    private static void MigrateV1(STOTOPDbContext ctx)
    {
        var isSqlServer = SeederHelper.IsSqlServer(ctx);
        EnsureOrgTypes(ctx, isSqlServer);
        SeedBUAuxiliary(ctx);

        // 修正经营单元项目的账套ID和组织ID（播种时可能尚无账套/组织数据）
        if (isSqlServer)
        {
            ctx.Database.ExecuteSqlRaw(@"
                UPDATE [FIN辅助核算项目] 
                SET [F账套ID] = ISNULL((SELECT TOP 1 FID FROM [FIN账套] WHERE [F是否默认] = 1), 1)
                WHERE [F辅助类型] = 'business_unit' AND [F账套ID] = 0;

                UPDATE [FIN辅助核算项目] 
                SET [F组织ID] = ISNULL((SELECT TOP 1 FID FROM [SYS组织架构] WHERE [F父ID] = 0 OR [F父ID] IS NULL), 1)
                WHERE [F辅助类型] = 'business_unit' AND [F组织ID] = 0;
            ");
        }

        // 同步 EXP品牌 → FIN辅助核算项目（express_brand 类型，全局 F账套ID=0, F组织ID=0）
        if (isSqlServer)
        {
            ctx.Database.ExecuteSqlRaw(@"
                -- 确保 express_brand 辅助核算类型存在且作用域为 global
                IF NOT EXISTS (SELECT 1 FROM [FIN辅助核算类型] WHERE [F名称] = N'express_brand')
                BEGIN
                    INSERT INTO [FIN辅助核算类型] ([F名称], [F作用域], [F状态], [F创建时间], [F更新时间])
                    VALUES (N'express_brand', N'global', 1, GETDATE(), GETDATE())
                END
                ELSE
                BEGIN
                    UPDATE [FIN辅助核算类型] SET [F作用域] = N'global' WHERE [F名称] = N'express_brand' AND [F作用域] <> N'global'
                END

                -- 确保 business_direction 辅助核算类型存在且作用域为 global
                IF NOT EXISTS (SELECT 1 FROM [FIN辅助核算类型] WHERE [F名称] = N'business_direction')
                BEGIN
                    INSERT INTO [FIN辅助核算类型] ([F名称], [F作用域], [F状态], [F创建时间], [F更新时间])
                    VALUES (N'business_direction', N'global', 1, GETDATE(), GETDATE())
                END
                ELSE
                BEGIN
                    UPDATE [FIN辅助核算类型] SET [F作用域] = N'global' WHERE [F名称] = N'business_direction' AND [F作用域] <> N'global'
                END

                -- 将 express_brand / business_direction 项目的 F账套ID/F组织ID 统一设为 0
                UPDATE [FIN辅助核算项目] SET [F账套ID] = 0, [F组织ID] = 0
                WHERE [F辅助类型] = N'express_brand' AND ([F账套ID] <> 0 OR [F组织ID] <> 0);

                UPDATE [FIN辅助核算项目] SET [F账套ID] = 0, [F组织ID] = 0
                WHERE [F辅助类型] = N'business_direction' AND ([F账套ID] <> 0 OR [F组织ID] <> 0);

                -- 从 EXP品牌 同步到 FIN辅助核算项目（全局：F账套ID=0, F组织ID=0）
                INSERT INTO [FIN辅助核算项目] ([F辅助类型], [F编码], [F名称], [F启用状态], [F账套ID], [F组织ID], [F来源类型], [F创建时间], [F更新时间])
                SELECT N'express_brand', b.[F编码], b.[F名称], 1, 0, 0, N'EXP品牌', GETDATE(), GETDATE()
                FROM [EXP品牌] b
                WHERE b.[F状态] = 1
                  AND NOT EXISTS (
                      SELECT 1 FROM [FIN辅助核算项目] i 
                      WHERE i.[F辅助类型] = N'express_brand' AND i.[F编码] = b.[F编码]
                  )
            ");
        }
    }

    // ========== EnsureOrgTypes ==========
    /// <summary>
    /// 幂等初始化 SYS组织类型 种子数据（9条）
    /// </summary>
    public static void EnsureOrgTypes(STOTOPDbContext context, bool isSqlServer = false)
    {
        if (isSqlServer && context.Database.ProviderName?.Contains("SqlServer") == false) return;

        if (context.Set<SysOrgType>().Any()) return;

        var now = DateTime.Now;
        var types = new List<SysOrgType>
        {
            new() { FID = 1, FCode = "GROUP",      FName = "集团",  FLevel = 1, FCanBindAccountSet = true,  FCanSwitch = true,  FIcon = "OfficeBuilding", FSortOrder = 1, FIsEnabled = true, FCreateTime = now },
            new() { FID = 2, FCode = "SUBSIDIARY", FName = "子公司", FLevel = 2, FCanBindAccountSet = true,  FCanSwitch = true,  FIcon = "Building",       FSortOrder = 2, FIsEnabled = true, FCreateTime = now },
            new() { FID = 3, FCode = "CENTER",     FName = "中心",  FLevel = 2, FCanBindAccountSet = false, FCanSwitch = false, FIcon = "Grid",           FSortOrder = 3, FIsEnabled = true, FCreateTime = now },
            new() { FID = 4, FCode = "BRANCH",     FName = "分公司", FLevel = 3, FCanBindAccountSet = true,  FCanSwitch = true,  FIcon = "Location",      FSortOrder = 4, FIsEnabled = true, FCreateTime = now },
            new() { FID = 5, FCode = "DEPT",       FName = "部门",  FLevel = 3, FCanBindAccountSet = false, FCanSwitch = false, FIcon = "Files",          FSortOrder = 5, FIsEnabled = true, FCreateTime = now },
            new() { FID = 6, FCode = "TEAM",       FName = "团组",  FLevel = 4, FCanBindAccountSet = false, FCanSwitch = false, FIcon = "User",           FSortOrder = 6, FIsEnabled = true, FCreateTime = now },
            new() { FID = 7, FCode = "NETWORK_POINT",      FName = "快递网点", FLevel = 3, FCanBindAccountSet = false, FCanSwitch = false, FIcon = "apartment",     FSortOrder = 7, FIsEnabled = true, FCreateTime = now },
            new() { FID = 8, FCode = "FRANCHISE_AREA",     FName = "承包区",  FLevel = 4, FCanBindAccountSet = false, FCanSwitch = false, FIcon = "location_city", FSortOrder = 8, FIsEnabled = true, FCreateTime = now },
            new() { FID = 9, FCode = "LAST_MILE_STATION",  FName = "末端驿站", FLevel = 4, FCanBindAccountSet = false, FCanSwitch = false, FIcon = "store",         FSortOrder = 9, FIsEnabled = true, FCreateTime = now },
        };

        context.Set<SysOrgType>().AddRange(types);
        // SysOrgType 主键是 ValueGeneratedNever（手动指定），不需要 IDENTITY_INSERT
        context.SaveChanges();
    }

    /// <summary>
    /// 幂等初始化辅助核算类型与经营单元默认项目
    /// </summary>
    private static void SeedBUAuxiliary(STOTOPDbContext context)
    {
        var now = DateTime.Now;
        var isSqlServer = SeederHelper.IsSqlServer(context);

        var existingTypes = context.Set<FinAuxiliaryType>().ToList();
        var existingNames = existingTypes.Select(t => t.FName).ToHashSet();

        // 预置所有 9 种类型，value 与前端 categories.value 保持一致
        // express_brand / business_direction 作用域为 global（合并原 V2/V3 修正）
        var presetTypeNames = new[] { "customer", "supplier", "employee", "project", "department", "business_unit", "express_brand", "outlet", "business_direction" };
        var missingNames = presetTypeNames.Where(n => !existingNames.Contains(n)).ToList();
        if (missingNames.Count == 0) return;

        long typeMaxId = existingTypes.Any() ? existingTypes.Max(t => t.FID) : 0;
        var typesToAdd = missingNames.Select((name, idx) => new FinAuxiliaryType
        {
            FID = typeMaxId + idx + 1,
            FName = name,
            FScope = (name == "express_brand" || name == "business_direction") ? "global" : "org_scoped",
            FStatus = 1,
            FCreatedTime = now,
            FUpdatedTime = now
        }).ToList();

        context.Set<FinAuxiliaryType>().AddRange(typesToAdd);
        SeederHelper.SaveWithIdentityInsert(context, "FIN辅助核算类型", isSqlServer);

        // 若经营单元类型是新增的，一并插入默认子项目
        if (missingNames.Contains("business_unit"))
        {
            // 获取默认账套ID和根组织ID
            long defaultAccountSetId = 0;
            long defaultOrgId = 0;
            if (isSqlServer)
            {
                defaultAccountSetId = context.Database
                    .SqlQueryRaw<long>("SELECT ISNULL((SELECT TOP 1 FID FROM [FIN账套] WHERE [F是否默认] = 1 ORDER BY FID), 0) AS [Value]")
                    .AsEnumerable()
                    .FirstOrDefault();
                defaultOrgId = context.Database
                    .SqlQueryRaw<long>("SELECT ISNULL((SELECT TOP 1 FID FROM [SYS组织架构] WHERE [F父ID] = 0 OR [F父ID] IS NULL ORDER BY CASE WHEN [F父ID] IS NULL THEN 0 ELSE 1 END, FID), 0) AS [Value]")
                    .AsEnumerable()
                    .FirstOrDefault();
            }

            var existingItems = context.Set<FinAuxiliaryItem>().ToList();
            long itemMaxId = existingItems.Any() ? existingItems.Max(i => i.FID) : 0;
            long buItemId = itemMaxId + 1;
            var buItems = new List<FinAuxiliaryItem>
            {
                new() { FID = buItemId++, FAuxType = "business_unit", FCode = "BU-TCMS", FName = "太仓美申", FEnableStatus = 1, FAccountSetId = defaultAccountSetId, FOrgId = defaultOrgId, FCreatedTime = now, FUpdatedTime = now },
                new() { FID = buItemId++, FAuxType = "business_unit", FCode = "BU-CQ", FName = "城区公司", FEnableStatus = 1, FAccountSetId = defaultAccountSetId, FOrgId = defaultOrgId, FCreatedTime = now, FUpdatedTime = now },
                new() { FID = buItemId++, FAuxType = "business_unit", FCode = "BU-NJ", FName = "南郊公司", FEnableStatus = 1, FAccountSetId = defaultAccountSetId, FOrgId = defaultOrgId, FCreatedTime = now, FUpdatedTime = now },
                new() { FID = buItemId++, FAuxType = "business_unit", FCode = "BU-LH", FName = "浏河公司", FEnableStatus = 1, FAccountSetId = defaultAccountSetId, FOrgId = defaultOrgId, FCreatedTime = now, FUpdatedTime = now },
                new() { FID = buItemId++, FAuxType = "business_unit", FCode = "BU-SX", FName = "沙溪公司", FEnableStatus = 1, FAccountSetId = defaultAccountSetId, FOrgId = defaultOrgId, FCreatedTime = now, FUpdatedTime = now },
                new() { FID = buItemId++, FAuxType = "business_unit", FCode = "BU-CG", FName = "出港业务", FEnableStatus = 1, FAccountSetId = defaultAccountSetId, FOrgId = defaultOrgId, FCreatedTime = now, FUpdatedTime = now },
            };
            context.Set<FinAuxiliaryItem>().AddRange(buItems);
            SeederHelper.SaveWithIdentityInsert(context, "FIN辅助核算项目", isSqlServer);
        }

        // 若业务方向类型是新增的，一并插入默认子项目
        if (missingNames.Contains("business_direction"))
        {
            // 获取默认账套ID和根组织ID
            long defaultAccountSetId = 0;
            long defaultOrgId = 0;
            if (isSqlServer)
            {
                defaultAccountSetId = context.Database
                    .SqlQueryRaw<long>("SELECT ISNULL((SELECT TOP 1 FID FROM [FIN账套] WHERE [F是否默认] = 1 ORDER BY FID), 0) AS [Value]")
                    .AsEnumerable()
                    .FirstOrDefault();
                defaultOrgId = context.Database
                    .SqlQueryRaw<long>("SELECT ISNULL((SELECT TOP 1 FID FROM [SYS组织架构] WHERE [F父ID] = 0 OR [F父ID] IS NULL ORDER BY CASE WHEN [F父ID] IS NULL THEN 0 ELSE 1 END, FID), 0) AS [Value]")
                    .AsEnumerable()
                    .FirstOrDefault();
            }

            var existingItems = context.Set<FinAuxiliaryItem>().ToList();
            long itemMaxId = existingItems.Any() ? existingItems.Max(i => i.FID) : 0;
            long bdItemId = itemMaxId + 1;
            var bdItems = new List<FinAuxiliaryItem>
            {
                new() { FID = bdItemId++, FAuxType = "business_direction", FCode = "IN", FName = "进港", FEnableStatus = 1, FAccountSetId = 0, FOrgId = 0, FCreatedTime = now, FUpdatedTime = now },
                new() { FID = bdItemId++, FAuxType = "business_direction", FCode = "OUT", FName = "出港", FEnableStatus = 1, FAccountSetId = 0, FOrgId = 0, FCreatedTime = now, FUpdatedTime = now },
                new() { FID = bdItemId++, FAuxType = "business_direction", FCode = "CMB", FName = "综合", FEnableStatus = 1, FAccountSetId = 0, FOrgId = 0, FCreatedTime = now, FUpdatedTime = now },
            };
            context.Set<FinAuxiliaryItem>().AddRange(bdItems);
            SeederHelper.SaveWithIdentityInsert(context, "FIN辅助核算项目", isSqlServer);
        }
    }
}
