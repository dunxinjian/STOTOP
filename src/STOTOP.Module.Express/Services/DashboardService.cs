using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Interfaces;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Services;

public class DashboardService : IDashboardService
{
    private readonly IRepository<ExpWaybill> _waybillRepo;
    private readonly IRepository<ExpBillingResult> _billingRepo;
    private readonly IRepository<ExpBillingCostBreakdown> _costBreakdownRepo;
    private readonly IRepository<ExpBrand> _brandRepo;
    private readonly IRepository<ExpQuotation> _quotationRepo;
    private readonly IRepository<ExpInvoice> _invoiceRepo;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<DashboardService> _logger;

    public DashboardService(
        IRepository<ExpWaybill> waybillRepo,
        IRepository<ExpBillingResult> billingRepo,
        IRepository<ExpBillingCostBreakdown> costBreakdownRepo,
        IRepository<ExpBrand> brandRepo,
        IRepository<ExpQuotation> quotationRepo,
        IRepository<ExpInvoice> invoiceRepo,
        IHttpContextAccessor httpContextAccessor,
        ILogger<DashboardService> logger)
    {
        _waybillRepo = waybillRepo;
        _billingRepo = billingRepo;
        _costBreakdownRepo = costBreakdownRepo;
        _brandRepo = brandRepo;
        _quotationRepo = quotationRepo;
        _invoiceRepo = invoiceRepo;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<DashboardDto> GetDashboardAsync(string? brandCode)
    {
        try
        {
            var today = DateTime.Today;
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var last30Days = today.AddDays(-30);

            var waybillQuery = _waybillRepo.Query();
            var orgId = (long)(_httpContextAccessor.HttpContext?.Items["CurrentOrgId"] ?? 0L);
            if (!string.IsNullOrWhiteSpace(brandCode))
                waybillQuery = waybillQuery.Where(w => w.FBrandCode == brandCode);

            // 今日单量
            var todayWaybills = await waybillQuery
                .CountAsync(w => w.FWaybillDate == today);

            // 本月单量
            var monthWaybills = await waybillQuery
                .CountAsync(w => w.FWaybillDate >= monthStart && w.FWaybillDate <= today);

            // 本月收入。显式限定 F计算状态=1，与报表/账单口径统一，不再依赖"失败行角色为NULL"的隐式约定排除失败行。
            var billingQuery = _billingRepo.Query().Where(b => b.FPartyRole == 1 && b.FCalcStatus == 1);
            if (orgId > 0)
                billingQuery = billingQuery.Where(b => b.FNetworkPointCode == orgId.ToString());
            if (!string.IsNullOrWhiteSpace(brandCode))
                billingQuery = billingQuery.Where(b => b.FBrandCode == brandCode);

            var monthRevenueList = await billingQuery
                .Where(b => b.FWaybillDate >= monthStart && b.FWaybillDate <= today)
                .Select(b => b.FChargeAmount ?? 0m)
                .ToListAsync();
            var monthRevenue = monthRevenueList.Sum();

            // 本月成本 — 先取数据到内存再 Sum，避免 EF Core 翻译问题
            var monthCostList = await (from br in billingQuery.Where(b => b.FWaybillDate >= monthStart && b.FWaybillDate <= today)
                                       join cbd in _costBreakdownRepo.Query() on br.FID equals cbd.FBillingResultId
                                       select cbd.FAmount).ToListAsync();
            var monthCost = monthCostList.Sum();

            var monthProfit = monthRevenue - monthCost;

            // 上月数据查询（环比计算）
            var lastMonthStart = monthStart.AddMonths(-1);
            var lastMonthEnd = monthStart.AddDays(-1);

            var lastMonthWaybills = await waybillQuery
                .CountAsync(w => w.FWaybillDate >= lastMonthStart && w.FWaybillDate <= lastMonthEnd);

            var lastMonthRevenueList = await billingQuery
                .Where(b => b.FWaybillDate >= lastMonthStart && b.FWaybillDate <= lastMonthEnd)
                .Select(b => b.FChargeAmount ?? 0m)
                .ToListAsync();
            var lastMonthRevenue = lastMonthRevenueList.Sum();

            var lastMonthCostList = await (from br in billingQuery.Where(b => b.FWaybillDate >= lastMonthStart && b.FWaybillDate <= lastMonthEnd)
                                           join cbd in _costBreakdownRepo.Query() on br.FID equals cbd.FBillingResultId
                                           select cbd.FAmount).ToListAsync();
            var lastMonthCost = lastMonthCostList.Sum();
            var lastMonthProfit = lastMonthRevenue - lastMonthCost;

            // 最近30天日趋势 — 先取原始数据，在内存中 GroupBy 和格式化
            var dailyTrendRaw = await (from w in waybillQuery.Where(w => w.FWaybillDate >= last30Days)
                                       join br in _billingRepo.Query().Where(b => b.FPartyRole == 1)
                                           on w.FWaybillNo equals br.FWaybillNo into brGroup
                                       from br in brGroup.DefaultIfEmpty()
                                       select new
                                       {
                                           Date = w.FWaybillDate,
                                           Revenue = br != null ? (br.FChargeAmount ?? 0m) : 0m
                                       }).ToListAsync();

            var dailyTrend = dailyTrendRaw
                .GroupBy(x => x.Date)
                .OrderBy(g => g.Key)
                .Select(g => new DailyTrendItem
                {
                    Date = g.Key.ToString("yyyy-MM-dd"),
                    WaybillCount = g.Count(),
                    Revenue = g.Sum(x => x.Revenue),
                    Cost = 0,
                    Profit = 0
                }).ToList();

            // 补充成本数据 — 先取原始数据到内存，再 GroupBy 和格式化
            var dailyCostRaw = await (from w in waybillQuery.Where(w => w.FWaybillDate >= last30Days)
                                      join br in _billingRepo.Query().Where(b => b.FPartyRole == 1) on w.FWaybillNo equals br.FWaybillNo
                                      join cbd in _costBreakdownRepo.Query() on br.FID equals cbd.FBillingResultId
                                      select new { Date = w.FWaybillDate, Cost = cbd.FAmount })
                                     .ToListAsync();
            var dailyCost = dailyCostRaw
                .GroupBy(x => x.Date.ToString("yyyy-MM-dd"))
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Cost));

            foreach (var item in dailyTrend)
            {
                item.Cost = dailyCost.GetValueOrDefault(item.Date, 0m);
                item.Profit = item.Revenue - item.Cost;
            }

            // 品牌分布 — 先取原始数据到内存，避免三元表达式在 GROUP BY 中翻译问题
            var brandDataRaw = await (from w in waybillQuery.Where(w => w.FWaybillDate >= monthStart)
                                      join b in _brandRepo.Query() on w.FBrandCode equals b.FCode into bGroup
                                      from b in bGroup.DefaultIfEmpty()
                                      select new { BrandCode = w.FBrandCode, BrandName = b != null ? b.FName : (string?)null })
                                     .ToListAsync();
            var brandData = brandDataRaw
                .GroupBy(x => new { x.BrandCode, BrandName = x.BrandName ?? "未知" })
                .Select(g => new { g.Key.BrandCode, g.Key.BrandName, WaybillCount = g.Count() })
                .ToList();

            var brandTotal = brandData.Sum(b => b.WaybillCount);
            var brandDistribution = brandData.Select(b => new BrandDistributionItem
            {
                BrandCode = b.BrandCode,
                BrandName = b.BrandName,
                WaybillCount = b.WaybillCount,
                Ratio = brandTotal > 0 ? Math.Round((decimal)b.WaybillCount / brandTotal * 100, 2) : 0
            }).OrderByDescending(b => b.WaybillCount).ToList();

            // TOP10客户 — 先只统计运单+计费，不 JOIN 报价方案，避免一客户多报价导致重复计数
            var topClientsRaw = await (from w in waybillQuery.Where(w => w.FWaybillDate >= monthStart && w.FWaybillDate <= today && w.FBusinessObjectId != null)
                                       join br in _billingRepo.Query().Where(b => b.FPartyRole == 1)
                                           on w.FWaybillNo equals br.FWaybillNo into brGroup
                                       from br in brGroup.DefaultIfEmpty()
                                       select new
                                       {
                                           ClientId = w.FBusinessObjectId!,
                                           ChargeAmount = br != null ? (br.FChargeAmount ?? 0m) : 0m
                                       }).ToListAsync();

            var grouped = topClientsRaw
                .GroupBy(x => x.ClientId)
                .OrderByDescending(g => g.Count())
                .Take(10)
                .ToList();

            // 根据 TOP10 的 ClientId 列表，单独查报价方案表取客户名称
            var clientIds = grouped.Select(g => g.Key).ToList();
            var nameMap = (await _quotationRepo.Query()
                .Where(q => q.FBusinessObjectId != null && clientIds.Contains(q.FBusinessObjectId))
                .ToListAsync())
                .GroupBy(q => q.FBusinessObjectId!)
                .ToDictionary(g => g.Key, g => g.First().FPlanName ?? "未知");

            var topClients = grouped
                .Select(g => new TopClientItem
                {
                    ClientId = g.Key,
                    ClientName = nameMap.GetValueOrDefault(g.Key, "未知"),
                    WaybillCount = g.Count(),
                    TotalCharge = g.Sum(x => x.ChargeAmount)
                }).ToList();

            // 异常预警
            var alerts = new List<AlertItem>();

            var unbilledCount = await waybillQuery.CountAsync(w => w.FBillingStatus == 0);
            if (unbilledCount > 0)
                alerts.Add(new AlertItem { Type = "unbilled", Message = "未计费运单", Count = unbilledCount });

            var errorBillingQuery = _billingRepo.Query().Where(b => b.FCalcStatus == 2);
            if (orgId > 0)
                errorBillingQuery = errorBillingQuery.Where(b => b.FNetworkPointCode == orgId.ToString());
            var errorCount = await errorBillingQuery.CountAsync();
            if (errorCount > 0)
                alerts.Add(new AlertItem { Type = "billing_error", Message = "计费异常", Count = errorCount });

            var pendingReviewQuery = _invoiceRepo.Query().Where(i => i.FReviewStatus == 0);
            var pendingReviewCount = await pendingReviewQuery.CountAsync();
            if (pendingReviewCount > 0)
                alerts.Add(new AlertItem { Type = "pending_review", Message = "待审核账单", Count = pendingReviewCount });

            return new DashboardDto
            {
                TodayWaybills = todayWaybills,
                MonthWaybills = monthWaybills,
                MonthRevenue = monthRevenue,
                MonthCost = monthCost,
                MonthProfit = monthProfit,
                MonthWaybillsChange = CalcChange(monthWaybills, lastMonthWaybills),
                MonthRevenueChange = CalcChange(monthRevenue, lastMonthRevenue),
                MonthCostChange = CalcChange(monthCost, lastMonthCost),
                MonthProfitChange = CalcChange(monthProfit, lastMonthProfit),
                DailyTrend = dailyTrend,
                BrandDistribution = brandDistribution,
                TopClients = topClients,
                Alerts = alerts
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取快递看板数据失败");
            return new DashboardDto();
        }
    }

    /// <summary>计算环比变化率，上期为0时返回null</summary>
    private static decimal? CalcChange(decimal current, decimal last)
    {
        if (last == 0) return null;
        return Math.Round((current - last) / last * 100, 1);
    }
}
