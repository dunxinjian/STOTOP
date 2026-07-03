using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.Finance.Services;

/// <summary>
/// 经营单元派生器（M6/R2 多租户阶段3B）。从 SYS网点公司 1:1 **物化派生** FIN经营单元(禁手工维护)。
/// 幂等:每网点公司 upsert 一条(按 FCompanyId)、名称/状态/租户随公司同步(公司停用→单元停用);
/// 公司已删的孤儿单元置停用(不硬删,保存量凭证/报表对 aux id 的引用不断链)。
/// 由 FinanceSeeder V17(平台作用域回填) 调用;将来 SYS网点公司 有运行时 CRUD 时经领域事件调本器(现只种子建,故运行时链暂 dormant)。
/// </summary>
public static class FinOperatingUnitDeriver
{
    public static void SyncAllFromOutletCompanies(STOTOPDbContext ctx)
    {
        var companies = ctx.Set<SysOutletCompany>().IgnoreQueryFilters().ToList();
        var units = ctx.Set<FinOperatingUnit>().IgnoreQueryFilters().AsTracking().ToList();
        var byCompany = units.ToDictionary(u => u.FCompanyId);

        foreach (var c in companies)
        {
            if (!byCompany.TryGetValue(c.FID, out var u))
            {
                u = new FinOperatingUnit { FCompanyId = c.FID, FCode = $"OU-{c.FID}" };
                ctx.Set<FinOperatingUnit>().Add(u);
            }
            u.FTenantId = c.FTenantId;
            u.FName = c.FName;
            u.FStatus = c.FStatus;          // 公司停用联动停用
            u.FUpdatedTime = DateTime.Now;
        }

        // 公司已删的孤儿单元 → 停用(保引用不断链)
        var companyIds = companies.Select(c => c.FID).ToHashSet();
        foreach (var u in units.Where(u => !companyIds.Contains(u.FCompanyId) && u.FStatus == 1))
        {
            u.FStatus = 0;
            u.FUpdatedTime = DateTime.Now;
        }

        ctx.SaveChanges();
    }
}
