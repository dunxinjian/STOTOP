using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Services;

public class FlowAnalysisService : IFlowAnalysisService
{
    private readonly IRepository<ExpWaybill> _waybillRepo;
    private readonly IRepository<ExpBillingResult> _billingRepo;
    private readonly IRepository<ExpProvince> _provinceRepo;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public FlowAnalysisService(
        IRepository<ExpWaybill> waybillRepo,
        IRepository<ExpBillingResult> billingRepo,
        IRepository<ExpProvince> provinceRepo,
        IHttpContextAccessor httpContextAccessor)
    {
        _waybillRepo = waybillRepo;
        _billingRepo = billingRepo;
        _provinceRepo = provinceRepo;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<List<FlowAnalysisDto>> GetFlowDistributionAsync(ReportQueryRequest request)
    {
        var waybillQuery = ApplyFilters(_waybillRepo.Query(), request);

        // 先获取省份列表用于内存中关联
        var provinces = await _provinceRepo.Query().ToDictionaryAsync(p => p.FID, p => p.FName);

        // 先在数据库层面聚合，避免在 group by 中使用三元表达式
        var query = from w in waybillQuery
                    join br in _billingRepo.Query().Where(b => b.FPartyRole == 1)
                        on w.FWaybillNo equals br.FWaybillNo into brGroup
                    from br in brGroup.DefaultIfEmpty()
                    where w.FReceiverProvinceId != null
                    group new { w, br } by w.FReceiverProvinceId into g
                    select new
                    {
                        ProvinceId = g.Key!.Value,
                        WaybillCount = g.Count(),
                        TotalWeight = g.Sum(x => x.w.FBillableWeight ?? 0m),
                        TotalCharge = g.Sum(x => x.br != null ? (x.br.FChargeAmount ?? 0m) : 0m)
                    };

        var data = await query.ToListAsync();
        var totalCount = data.Sum(d => d.WaybillCount);

        // 在内存中关联省份名称并计算派生字段
        return data.Select(d => new FlowAnalysisDto
        {
            Province = provinces.TryGetValue(d.ProvinceId, out var name) ? name : "未知",
            WaybillCount = d.WaybillCount,
            Ratio = totalCount > 0 ? Math.Round((decimal)d.WaybillCount / totalCount * 100, 2) : 0,
            TotalWeight = d.TotalWeight,
            AvgWeight = d.WaybillCount > 0 ? Math.Round(d.TotalWeight / d.WaybillCount, 3) : 0,
            TotalCharge = d.TotalCharge,
            AvgPrice = d.WaybillCount > 0 ? Math.Round(d.TotalCharge / d.WaybillCount, 2) : 0
        }).OrderByDescending(d => d.WaybillCount).ToList();
    }

    public async Task<List<FlowTrendDto>> GetFlowTrendAsync(ReportQueryRequest request, string granularity)
    {
        var waybillQuery = ApplyFilters(_waybillRepo.Query(), request);

        // 先从数据库获取原始数据，避免 EF Core 无法翻译自定义方法 GetDateKey
        var rawData = await (from w in waybillQuery
                             join br in _billingRepo.Query().Where(b => b.FPartyRole == 1)
                                 on w.FWaybillNo equals br.FWaybillNo into brGroup
                             from br in brGroup.DefaultIfEmpty()
                             select new
                             {
                                 w.FWaybillDate,
                                 ChargeAmount = br != null ? (br.FChargeAmount ?? 0m) : 0m
                             }).ToListAsync();

        // 在内存中按日期分组计算
        return rawData
            .GroupBy(x => GetDateKey(x.FWaybillDate, granularity))
            .OrderBy(g => g.Key)
            .Select(g => new FlowTrendDto
            {
                Date = g.Key,
                WaybillCount = g.Count(),
                TotalCharge = g.Sum(x => x.ChargeAmount)
            })
            .ToList();
    }

    private static IQueryable<ExpWaybill> ApplyFilters(IQueryable<ExpWaybill> query, ReportQueryRequest request)
    {
        if (request.DateFrom.HasValue)
            query = query.Where(w => w.FWaybillDate >= request.DateFrom.Value);
        if (request.DateTo.HasValue)
            query = query.Where(w => w.FWaybillDate <= request.DateTo.Value);
        if (!string.IsNullOrWhiteSpace(request.BrandCode))
            query = query.Where(w => w.FBrandCode == request.BrandCode);
        if (!string.IsNullOrWhiteSpace(request.ClientId))
            query = query.Where(w => w.FBusinessObjectId == request.ClientId);
        if (request.ProvinceId.HasValue)
            query = query.Where(w => w.FReceiverProvinceId == request.ProvinceId.Value);
        return query;
    }

    private static string GetDateKey(DateTime date, string granularity)
    {
        return granularity?.ToLower() switch
        {
            // ISOWeek 处理跨年周：12月末几天可能属于次年第1周，年份必须取 ISO 周年
            "week" => $"{ISOWeek.GetYear(date)}-W{ISOWeek.GetWeekOfYear(date):D2}",
            "month" => date.ToString("yyyy-MM"),
            _ => date.ToString("yyyy-MM-dd")
        };
    }
}
