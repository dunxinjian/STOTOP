using Microsoft.EntityFrameworkCore;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.System.Entities;

namespace STOTOP.Module.Finance.Services;

/// <summary>
/// 经营单元派生器（M6/R2 多租户阶段3B/3C）。从 SYS网点公司 1:1 **物化派生** FIN经营单元(禁手工维护)+ 建到遗留 business_unit aux 的**单向**交叉引用桥(OU→aux,存 OU 侧)。
/// 幂等:每网点公司 upsert 一条(按 FCompanyId)、名称/状态/租户随公司同步(公司停用→单元停用);公司已删的孤儿单元置停用(不硬删,保存量凭证/报表对 aux id 的引用不断链)。
/// **调用时机(关键)**:交叉引用桥依赖 business_unit aux 已存在。aux 由 BasicDataSeeder(BasicData tier) 播种,**晚于** Finance tier——故:
///   ① FinanceSeeder V17/V18(Finance tier·平台作用域):建 OU + 试桥,但 fresh 库此时 aux 尚未播种→桥暂空(无害);
///   ② BasicDataSeeder V1(BasicData tier·SeedBUAuxiliary 之后):aux 已在→在此重跑本器把桥补齐(fresh 库唯一有效建桥点)。
/// 两处皆幂等,各覆盖 existing-DB 升级 / fresh-DB 首建 一路。将来 SYS网点公司 有运行时 CRUD 时经领域事件亦调本器。
/// </summary>
public static class FinOperatingUnitDeriver
{
    public static void SyncAllFromOutletCompanies(STOTOPDbContext ctx)
    {
        var companies = ctx.Set<SysOutletCompany>().IgnoreQueryFilters().ToList();
        var units = ctx.Set<FinOperatingUnit>().IgnoreQueryFilters().AsTracking().ToList();
        var byCompany = units.ToDictionary(u => u.FCompanyId);

        // 阶段3C 交叉引用桥：网点公司 → 遗留 business_unit aux。
        // 名不一致(网点公司"城区子公司" vs aux"城区公司"),按 (租户,规范名) 匹配——规范名 = 公司名去"子"(子公司→公司)。
        // 只桥网点公司级 aux;出港业务(方向)/太仓美申(区域) 无网点公司故不在此表、天然不桥。
        // 同租户同规范名多 aux(账套维度可重名,无唯一约束)时取 **最小 FID** 确定性择一(勿静默随机)。
        var auxByKey = ctx.Set<FinAuxiliaryItem>().IgnoreQueryFilters()
            .Where(a => a.FAuxType == "business_unit")
            .Select(a => new { a.FID, a.FTenantId, a.FName })
            .ToList()
            .GroupBy(a => (a.FTenantId, a.FName))
            .ToDictionary(g => g.Key, g => g.OrderBy(a => a.FID).First().FID);

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
            u.FSourceType = "SYS网点公司";
            var legacyName = c.FName.Replace("子公司", "公司");
            u.FSourceLegacyAuxId = auxByKey.TryGetValue((c.FTenantId, legacyName), out var auxId) ? auxId : null;
            u.FUpdatedTime = DateTime.Now;
        }

        // 公司已删的孤儿单元 → 停用(保引用不断链)
        var companyIds = companies.Select(c => c.FID).ToHashSet();
        foreach (var u in units.Where(u => !companyIds.Contains(u.FCompanyId) && u.FStatus == 1))
        {
            u.FStatus = 0;
            u.FUpdatedTime = DateTime.Now;
        }

        // 桥单向存于 OU 侧(F来源业务单元ID)即足够:报表按此展开/上卷。
        // **不**反标 business_unit aux 的 F来源类型——否则 AuxiliaryService.UpdateItemByAccountSetAsync 的"外部来源项不可改名"守卫会
        // 静默冻结这 4 个经营单元 aux 的改名(且 OU 名随公司同步、aux 名冻结 → 漂移)。provenance 由 OU.F来源业务单元ID 单向承载即可。
        ctx.SaveChanges();
    }
}
