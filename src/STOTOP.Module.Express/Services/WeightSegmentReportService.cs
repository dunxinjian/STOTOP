using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Services;

public class WeightSegmentReportService : IWeightSegmentReportService
{
    private readonly IRepository<ExpWaybill> _waybillRepo;
    private readonly IRepository<ExpBillingResult> _billingRepo;
    private readonly IHttpContextAccessor _httpContextAccessor;

    private static readonly (string Name, decimal Min, decimal Max)[] Segments =
    {
        ("0-0.5kg", 0m, 0.5m),
        ("0.5-1kg", 0.5m, 1m),
        ("1-2kg", 1m, 2m),
        ("2-3kg", 2m, 3m),
        ("3-5kg", 3m, 5m),
        ("5-10kg", 5m, 10m),
        ("10-20kg", 10m, 20m),
        ("20+kg", 20m, decimal.MaxValue)
    };

    public WeightSegmentReportService(
        IRepository<ExpWaybill> waybillRepo,
        IRepository<ExpBillingResult> billingRepo,
        IHttpContextAccessor httpContextAccessor)
    {
        _waybillRepo = waybillRepo;
        _billingRepo = billingRepo;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<List<WeightSegmentReportDto>> GetWeightDistributionAsync(ReportQueryRequest request)
    {
        var waybillQuery = ApplyFilters(_waybillRepo.Query(), request)
            .Where(w => w.FBillableWeight != null);

        var data = await (from w in waybillQuery
                          join br in _billingRepo.Query().Where(b => b.FPartyRole == 1)
                              on w.FWaybillNo equals br.FWaybillNo into brGroup
                          from br in brGroup.DefaultIfEmpty()
                          select new
                          {
                              Weight = w.FBillableWeight!.Value,
                              Charge = br != null ? (br.FChargeAmount ?? 0m) : 0m
                          }).ToListAsync();

        var totalCount = data.Count;

        return Segments.Select(seg =>
        {
            var items = data.Where(d => d.Weight >= seg.Min && d.Weight < seg.Max).ToList();
            var count = items.Count;
            var totalWeight = items.Sum(i => i.Weight);
            var totalCharge = items.Sum(i => i.Charge);

            return new WeightSegmentReportDto
            {
                SegmentName = seg.Name,
                WaybillCount = count,
                Ratio = totalCount > 0 ? Math.Round((decimal)count / totalCount * 100, 2) : 0,
                TotalWeight = totalWeight,
                TotalCharge = totalCharge,
                AvgPrice = count > 0 ? Math.Round(totalCharge / count, 2) : 0,
                AvgPricePerKg = totalWeight > 0 ? Math.Round(totalCharge / totalWeight, 2) : 0
            };
        }).ToList();
    }

    public async Task<List<WeightTrendDto>> GetAvgWeightTrendAsync(ReportQueryRequest request, string granularity)
    {
        var waybillQuery = ApplyFilters(_waybillRepo.Query(), request)
            .Where(w => w.FBillableWeight != null);

        // 先担必要字段到内存，再用C#计算日期键（GetDateKey无法被EF Core SQL翻译）
        var rawData = await waybillQuery
            .Select(w => new { w.FWaybillDate, BillableWeight = w.FBillableWeight!.Value })
            .ToListAsync();

        var result = rawData
            .GroupBy(w => GetDateKey(w.FWaybillDate, granularity))
            .OrderBy(g => g.Key)
            .Select(g => new WeightTrendDto
            {
                Date = g.Key,
                AvgWeight = g.Average(x => x.BillableWeight)
            })
            .ToList();

        return result;
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
            "week" => $"{date.Year}-W{CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(date, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday):D2}",
            "month" => date.ToString("yyyy-MM"),
            _ => date.ToString("yyyy-MM-dd")
        };
    }
}
