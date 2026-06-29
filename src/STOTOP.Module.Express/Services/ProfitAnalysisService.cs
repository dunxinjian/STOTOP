using System.Globalization;
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Services;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Entities;

namespace STOTOP.Module.Express.Services;

public class ProfitAnalysisService : IProfitAnalysisService
{
    private readonly IRepository<ExpBillingResult> _billingRepo;
    private readonly IRepository<ExpProvince> _provinceRepo;
    private readonly STOTOPDbContext _dbContext;
    private readonly IOrgContextAccessor _orgContextAccessor;
    private readonly IConfiguration _configuration;

    public ProfitAnalysisService(
        IRepository<ExpBillingResult> billingRepo,
        IRepository<ExpProvince> provinceRepo,
        STOTOPDbContext dbContext,
        IOrgContextAccessor orgContextAccessor,
        IConfiguration configuration)
    {
        _billingRepo = billingRepo;
        _provinceRepo = provinceRepo;
        _dbContext = dbContext;
        _orgContextAccessor = orgContextAccessor;
        _configuration = configuration;
    }

    /// <summary>
    /// 寄件省ID。省名可经 Express:SenderProvinceName 配置（默认江苏），用于"省内"判定。
    /// </summary>
    private async Task<int> GetSenderProvinceIdAsync()
    {
        var name = _configuration["Express:SenderProvinceName"];
        if (string.IsNullOrWhiteSpace(name))
            name = "江苏";
        return await _provinceRepo.Query()
            .Where(p => p.FName == name)
            .Select(p => (int?)p.FID)
            .FirstOrDefaultAsync() ?? 10;
    }

    /// <summary>毛利率(%)：应收≤0 时无意义，返回 null</summary>
    private static decimal? CalcProfitRate(decimal charge, decimal profit)
        => charge > 0 ? Math.Round(profit / charge * 100, 2) : null;

    public async Task<List<ProfitByClientDto>> GetProfitByClientAsync(ReportQueryRequest request)
    {
        var query = ApplyBillingFilters(_billingRepo.Query(), request);

        // 直接从计费结果按业务对象分组，使用 FTotalCost 预计算成本
        var data = await (
            from br in query
            group br by new { br.FClientType, br.FPartyClientId } into g
            select new
            {
                ClientId = g.Key.FPartyClientId,
                ClientName = g.Max(x => x.FPartyClientName),
                ClientType = g.Key.FClientType,
                WaybillCount = g.Count(),
                TotalWeight = g.Sum(x => x.FBillableWeight ?? 0m),
                TotalCharge = g.Sum(x => x.FChargeAmount ?? 0m),
                TotalCost = g.Sum(x => x.FTotalCost),
                ZeroChargeCount = g.Sum(x => (x.FChargeAmount ?? 0m) <= 0m ? 1 : 0)
            }
        ).ToListAsync();

        var clientNameMap = await ResolveClientNamesAsync(data.Select(d => ((string?)d.ClientType, (string?)d.ClientId)));

        return data.Select(d =>
        {
            var profit = d.TotalCharge - d.TotalCost;
            var clientName = GetDisplayName(d.ClientName, d.ClientType, d.ClientId, clientNameMap);
            return new ProfitByClientDto
            {
                ClientId = d.ClientId,
                ClientName = clientName,
                ClientType = ParseClientType(d.ClientType),
                WaybillCount = d.WaybillCount,
                TotalWeight = d.TotalWeight,
                TotalCharge = d.TotalCharge,
                TotalCost = d.TotalCost,
                Profit = profit,
                ProfitRate = CalcProfitRate(d.TotalCharge, profit),
                AvgPrice = d.WaybillCount > 0 ? Math.Round(d.TotalCharge / d.WaybillCount, 2) : 0,
                AvgProfit = d.WaybillCount > 0 ? Math.Round(profit / d.WaybillCount, 2) : 0,
                ZeroChargeCount = d.ZeroChargeCount
            };
        }).OrderByDescending(d => d.Profit).ToList();
    }

    public async Task<List<ProfitByShopDto>> GetProfitByShopAsync(ReportQueryRequest request)
    {
        var query = ApplyBillingFilters(_billingRepo.Query(), request);

        var data = await (
            from br in query
            group br by new { br.FClientType, br.FPartyClientId, br.FQuotationCode } into g
            select new
            {
                g.Key.FClientType,
                g.Key.FPartyClientId,
                g.Key.FQuotationCode,
                ClientName = g.Max(x => x.FPartyClientName),
                WaybillCount = g.Count(),
                TotalWeight = g.Sum(x => x.FBillableWeight ?? 0m),
                TotalCharge = g.Sum(x => x.FChargeAmount ?? 0m),
                TotalCost = g.Sum(x => x.FTotalCost)
            }
        ).ToListAsync();

        var clientNameMap = await ResolveClientNamesAsync(data.Select(d => ((string?)d.FClientType, (string?)d.FPartyClientId)));

        return data.Select(d =>
        {
            var profit = d.TotalCharge - d.TotalCost;
            var clientName = GetDisplayName(d.ClientName, d.FClientType, d.FPartyClientId, clientNameMap);
            return new ProfitByShopDto
            {
                ShopName = d.FQuotationCode ?? clientName,
                ClientName = clientName,
                WaybillCount = d.WaybillCount,
                TotalWeight = d.TotalWeight,
                TotalCharge = d.TotalCharge,
                TotalCost = d.TotalCost,
                Profit = profit,
                ProfitRate = CalcProfitRate(d.TotalCharge, profit)
            };
        }).OrderByDescending(d => d.Profit).ToList();
    }

    public async Task<List<ProfitTrendDto>> GetProfitTrendAsync(ReportQueryRequest request, string granularity)
    {
        // GetDateKey 无法被 EF 翻译（FlowAnalysisService 已踩过此坑）：
        // 先在 SQL 层按"日"聚合压缩结果集，再在内存按粒度二次合并。
        var query = ApplyBillingFilters(_billingRepo.Query(), request)
            .Where(b => b.FWaybillDate != null);

        var dailyData = await (
            from br in query
            group br by br.FWaybillDate!.Value.Date into g
            select new
            {
                Date = g.Key,
                TotalCharge = g.Sum(x => x.FChargeAmount ?? 0m),
                TotalCost = g.Sum(x => x.FTotalCost)
            }
        ).ToListAsync();

        return dailyData
            .GroupBy(d => GetDateKey(d.Date, granularity))
            .Select(g =>
            {
                var charge = g.Sum(x => x.TotalCharge);
                var cost = g.Sum(x => x.TotalCost);
                var profit = charge - cost;
                return new ProfitTrendDto
                {
                    Date = g.Key,
                    TotalCharge = charge,
                    TotalCost = cost,
                    Profit = profit,
                    ProfitRate = CalcProfitRate(charge, profit)
                };
            })
            .OrderBy(d => d.Date)
            .ToList();
    }

    public async Task<List<ProfitByIntermediaryDto>> GetProfitByIntermediaryAsync(ReportQueryRequest request)
    {
        var query = _billingRepo.Query().Where(b => b.FPartyRole == 2 && b.FCalcStatus == 1);
        query = ApplyBillingDateAndBrandFilters(query, request);

        var data = await (
            from br in query
            group br by new { br.FClientType, br.FPartyClientId } into g
            select new
            {
                ClientId = g.Key.FPartyClientId,
                ClientName = g.Max(x => x.FPartyClientName),
                ClientType = g.Key.FClientType,
                ChainLevel = g.Max(x => x.FChainLevel ?? 0),
                WaybillCount = g.Count(),
                TotalWeight = g.Sum(x => x.FBillableWeight ?? 0m),
                TotalCharge = g.Sum(x => x.FChargeAmount ?? 0m),
                TotalCost = g.Sum(x => x.FTotalCost)
            }
        ).ToListAsync();

        var clientNameMap = await ResolveClientNamesAsync(data.Select(d => ((string?)d.ClientType, (string?)d.ClientId)));

        return data.Select(d =>
        {
            var profit = d.TotalCharge - d.TotalCost;
            var clientName = GetDisplayName(d.ClientName, d.ClientType, d.ClientId, clientNameMap);
            return new ProfitByIntermediaryDto
            {
                ClientId = d.ClientId,
                ClientName = clientName,
                ClientType = ParseClientType(d.ClientType),
                ChainLevel = d.ChainLevel,
                WaybillCount = d.WaybillCount,
                TotalWeight = d.TotalWeight,
                DownstreamRevenue = d.TotalCharge,
                UpstreamCost = d.TotalCost,
                Profit = profit,
                ProfitRate = CalcProfitRate(d.TotalCharge, profit),
                AvgProfit = d.WaybillCount > 0 ? Math.Round(profit / d.WaybillCount, 2) : 0
            };
        }).OrderByDescending(x => x.Profit).ToList();
    }

    public async Task<List<ProfitBySalesmanDto>> GetProfitBySalesmanAsync(ReportQueryRequest request)
    {
        var query = _billingRepo.Query().Where(b => b.FPartyRole == 3 && b.FCalcStatus == 1);
        query = ApplyBillingDateAndBrandFilters(query, request);

        var data = await (
            from br in query
            group br by new { br.FClientType, br.FPartyClientId } into g
            select new
            {
                SalesmanId = g.Key.FPartyClientId,
                SalesmanName = g.Max(x => x.FPartyClientName),
                SalesmanType = g.Key.FClientType,
                WaybillCount = g.Count(),
                TotalWeight = g.Sum(x => x.FBillableWeight ?? 0m),
                // 仅统计已计算的佣金；不能用应收金额兜底，否则未配佣金规则的单会按全额虚增提成
                CommissionIncome = g.Sum(x => x.FCommissionAmount ?? 0m)
            }
        ).ToListAsync();

        var clientNameMap = await ResolveClientNamesAsync(data.Select(d => ((string?)d.SalesmanType, (string?)d.SalesmanId)));

        return data.Select(d => new ProfitBySalesmanDto
        {
            SalesmanId = d.SalesmanId,
            SalesmanName = GetDisplayName(d.SalesmanName, d.SalesmanType, d.SalesmanId, clientNameMap),
            NetworkPointId = null,
            WaybillCount = d.WaybillCount,
            TotalWeight = d.TotalWeight,
            CommissionIncome = d.CommissionIncome,
            Profit = d.CommissionIncome,
            AvgCommission = d.WaybillCount > 0 ? Math.Round(d.CommissionIncome / d.WaybillCount, 2) : 0
        }).OrderByDescending(x => x.Profit).ToList();
    }

    public async Task<List<ProfitByRegionDto>> GetProfitByRegionAsync(ReportQueryRequest request)
    {
        var query = ApplyBillingFilters(_billingRepo.Query(), request);

        var senderProvinceId = await GetSenderProvinceIdAsync();

        var data = await (
            from br in query
            group br by br.FDestinationProvinceId into g
            select new
            {
                ProvinceId = g.Key,
                WaybillCount = g.Count(),
                TotalWeight = g.Sum(x => x.FBillableWeight ?? 0m),
                TotalCharge = g.Sum(x => x.FChargeAmount ?? 0m),
                TotalCost = g.Sum(x => x.FTotalCost)
            }
        ).ToListAsync();

        return data
            .GroupBy(p => RegionMapping.GetRegion(p.ProvinceId, senderProvinceId))
            .Select(g =>
            {
                var charge = g.Sum(x => x.TotalCharge);
                var cost = g.Sum(x => x.TotalCost);
                var profit = charge - cost;
                var count = g.Sum(x => x.WaybillCount);
                var weight = g.Sum(x => x.TotalWeight);
                var regionOrder = Array.IndexOf(RegionMapping.AllRegions, g.Key);
                if (regionOrder < 0) regionOrder = RegionMapping.AllRegions.Length;
                return new ProfitByRegionDto
                {
                    Region = g.Key,
                    RegionOrder = regionOrder,
                    WaybillCount = count,
                    TotalWeight = weight,
                    TotalCharge = charge,
                    TotalCost = cost,
                    Profit = profit,
                    ProfitRate = CalcProfitRate(charge, profit),
                    AvgWeight = count > 0 ? Math.Round(weight / count, 3) : 0,
                    AvgProfit = count > 0 ? Math.Round(profit / count, 2) : 0
                };
            })
            .OrderBy(x => x.RegionOrder)
            .ToList();
    }

    public async Task<List<ProfitByProvinceDto>> GetProfitByProvinceAsync(ReportQueryRequest request, string? region)
    {
        var senderProvinceId = await GetSenderProvinceIdAsync();

        var query = ApplyBillingFilters(_billingRepo.Query(), request);

        // 如果指定了大区，过滤省份。各分支必须与父级 GetRegion 的归类口径一致，否则父子合计对不上。
        if (!string.IsNullOrWhiteSpace(region))
        {
            if (region == RegionMapping.RegionLocal)
            {
                query = query.Where(br => br.FDestinationProvinceId == senderProvinceId);
            }
            else if (region == RegionMapping.RegionUnknown)
            {
                // 未知 = 省份ID非法，或未映射到任何大区（寄件省除外，它归"省内"）
                var mappedIds = RegionMapping.MappedProvinceIds.ToList();
                query = query.Where(br => br.FDestinationProvinceId <= 0 ||
                    (br.FDestinationProvinceId != senderProvinceId && !mappedIds.Contains(br.FDestinationProvinceId)));
            }
            else
            {
                var provinceIds = RegionMapping.GetProvinceIdsByRegion(region);
                if (provinceIds.Count == 0)
                    return new List<ProfitByProvinceDto>();
                // 寄件省在父级归"省内"，此处必须排除，避免同时出现在"省内"和所属大区下
                query = query.Where(br => provinceIds.Contains(br.FDestinationProvinceId) &&
                    br.FDestinationProvinceId != senderProvinceId);
            }
        }

        var data = await (
            from br in query
            group br by br.FDestinationProvinceId into g
            select new
            {
                ProvinceId = g.Key,
                WaybillCount = g.Count(),
                TotalWeight = g.Sum(x => x.FBillableWeight ?? 0m),
                TotalCharge = g.Sum(x => x.FChargeAmount ?? 0m),
                TotalCost = g.Sum(x => x.FTotalCost)
            }
        ).ToListAsync();

        // 获取省份名称
        var provinceIdList = data.Where(p => p.ProvinceId > 0).Select(p => p.ProvinceId).Distinct().ToList();
        var provinces = await _provinceRepo.Query()
            .Where(p => provinceIdList.Contains(p.FID))
            .Select(p => new { p.FID, p.FName })
            .ToDictionaryAsync(p => p.FID, p => p.FName);

        return data.Select(p =>
        {
            var provinceId = p.ProvinceId;
            provinces.TryGetValue(provinceId, out var provinceName);
            var charge = p.TotalCharge;
            var cost = p.TotalCost;
            var profit = charge - cost;
            return new ProfitByProvinceDto
            {
                ProvinceId = provinceId,
                ProvinceName = provinceName ?? $"未知省份({provinceId})",
                Region = RegionMapping.GetRegion(provinceId, senderProvinceId),
                WaybillCount = p.WaybillCount,
                TotalWeight = p.TotalWeight,
                TotalCharge = charge,
                TotalCost = cost,
                Profit = profit,
                ProfitRate = CalcProfitRate(charge, profit),
                AvgProfit = p.WaybillCount > 0 ? Math.Round(profit / p.WaybillCount, 2) : 0
            };
        }).OrderByDescending(p => p.Profit).ToList();
    }

    private static readonly string[] WeightSegmentNames = { "0-0.5kg", "0.5-1kg", "1-2kg", "2-3kg", "3-5kg", "5kg+", "无重量" };

    public async Task<List<ProfitByWeightSegmentDto>> GetProfitByWeightSegmentAsync(ReportQueryRequest request)
    {
        var query = ApplyBillingFilters(_billingRepo.Query(), request);

        var data = await (
            from br in query
            group br by (
                // 重量缺失或非正数单独成段，避免混入 0-0.5kg 拉低段内指标
                br.FBillableWeight == null || br.FBillableWeight <= 0m ? 6 :
                br.FBillableWeight < 0.5m ? 0 :
                br.FBillableWeight < 1m ? 1 :
                br.FBillableWeight < 2m ? 2 :
                br.FBillableWeight < 3m ? 3 :
                br.FBillableWeight < 5m ? 4 : 5
            ) into g
            select new
            {
                SegmentIndex = g.Key,
                WaybillCount = g.Count(),
                TotalWeight = g.Sum(x => x.FBillableWeight ?? 0m),
                TotalCharge = g.Sum(x => x.FChargeAmount ?? 0m),
                TotalCost = g.Sum(x => x.FTotalCost)
            }
        ).ToListAsync();

        return data.Select(d =>
        {
            var profit = d.TotalCharge - d.TotalCost;
            var segmentName = d.SegmentIndex >= 0 && d.SegmentIndex < WeightSegmentNames.Length
                ? WeightSegmentNames[d.SegmentIndex]
                : "未知";
            return new ProfitByWeightSegmentDto
            {
                WeightSegment = segmentName,
                SegmentOrder = d.SegmentIndex,
                WaybillCount = d.WaybillCount,
                TotalWeight = d.TotalWeight,
                TotalCharge = d.TotalCharge,
                TotalCost = d.TotalCost,
                Profit = profit,
                ProfitRate = CalcProfitRate(d.TotalCharge, profit),
                AvgCharge = d.WaybillCount > 0 ? Math.Round(d.TotalCharge / d.WaybillCount, 2) : 0,
                AvgCost = d.WaybillCount > 0 ? Math.Round(d.TotalCost / d.WaybillCount, 2) : 0,
                AvgProfit = d.WaybillCount > 0 ? Math.Round(profit / d.WaybillCount, 2) : 0
            };
        }).OrderBy(x => x.SegmentOrder).ToList();
    }

    /// <summary>
    /// 对 ExpBillingResult 应用报表过滤条件（Role=1, CalcStatus=1 + 日期/品牌/客户/省份）
    /// </summary>
    private IQueryable<ExpBillingResult> ApplyBillingFilters(IQueryable<ExpBillingResult> query, ReportQueryRequest request)
    {
        query = ApplyDateBrandProvinceFilters(query.Where(b => b.FPartyRole == 1 && b.FCalcStatus == 1), request);
        if (!string.IsNullOrWhiteSpace(request.ClientId))
            query = query.Where(b => b.FPartyClientId == request.ClientId);
        return query;
    }

    /// <summary>
    /// 对 ExpBillingResult 应用日期和品牌过滤（用于 Role=2/3 的查询，不限定 Role）。
    /// 业务对象筛选不能直接匹配本行 FPartyClientId（那是中间人/业务员自己的编号），
    /// 而是经运单号关联 Role=1 应收行的业务对象。
    /// </summary>
    private IQueryable<ExpBillingResult> ApplyBillingDateAndBrandFilters(IQueryable<ExpBillingResult> query, ReportQueryRequest request)
    {
        query = ApplyDateBrandProvinceFilters(query, request);
        if (!string.IsNullOrWhiteSpace(request.ClientId))
        {
            var clientId = request.ClientId;
            query = query.Where(b => _billingRepo.Query().Any(r =>
                r.FPartyRole == 1 && r.FCalcStatus == 1 &&
                r.FWaybillNo == b.FWaybillNo &&
                r.FPartyClientId == clientId));
        }
        return query;
    }

    private static IQueryable<ExpBillingResult> ApplyDateBrandProvinceFilters(IQueryable<ExpBillingResult> query, ReportQueryRequest request)
    {
        if (request.DateFrom.HasValue)
        {
            var from = request.DateFrom.Value.Date;
            query = query.Where(b => b.FWaybillDate >= from);
        }
        if (request.DateTo.HasValue)
        {
            // 结束日含当天：运单日期可能带时间分量，用次日零点开区间
            var toExclusive = request.DateTo.Value.Date.AddDays(1);
            query = query.Where(b => b.FWaybillDate < toExclusive);
        }
        if (!string.IsNullOrWhiteSpace(request.BrandCode))
            query = query.Where(b => b.FBrandCode == request.BrandCode);
        if (request.ProvinceId.HasValue)
            query = query.Where(b => b.FDestinationProvinceId == request.ProvinceId.Value);
        return query;
    }

    private static int ParseClientType(string? clientType)
    {
        return clientType switch
        {
            "KH" => 1, // 客户(电商)
            "DL" => 2, // 代理(散户)
            "WD" => 3, // 网点
            "YW" => 4, // 业务员
            "CB" => 5, // 承包区
            "YZ" => 6, // 驿站
            _ => 0
        };
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

    public async Task<ProfitFilterOptionsDto> GetFilterOptionsAsync()
    {
        var brands = await _billingRepo.Query()
            .Where(b => b.FPartyRole == 1 && b.FCalcStatus == 1 && b.FBrandCode != null)
            .GroupBy(b => b.FBrandCode)
            .Select(g => new FilterOptionItem { Value = g.Key!, Label = g.Key! })
            .ToListAsync();

        var clientRows = await _billingRepo.Query()
            .Where(b => b.FPartyRole == 1 && b.FCalcStatus == 1)
            .GroupBy(b => new { b.FClientType, b.FPartyClientId })
            .Select(g => new
            {
                ClientType = g.Key.FClientType,
                ClientId = g.Key.FPartyClientId,
                ClientName = g.Max(x => x.FPartyClientName)
            })
            .ToListAsync();

        var clientNameMap = await ResolveClientNamesAsync(clientRows.Select(c => ((string?)c.ClientType, (string?)c.ClientId)));
        var clients = clientRows
            .Select(c => new FilterOptionItem
            {
                Value = c.ClientId,
                Label = GetDisplayName(c.ClientName, c.ClientType, c.ClientId, clientNameMap)
            })
            .OrderBy(c => c.Label)
            .ToList();

        return new ProfitFilterOptionsDto { Brands = brands, Clients = clients };
    }

    private static string GetDisplayName(
        string? storedName,
        string? clientType,
        string clientId,
        Dictionary<(string Type, string Id), string> clientNameMap)
    {
        if (!string.IsNullOrWhiteSpace(storedName))
            return storedName;
        if (!string.IsNullOrWhiteSpace(clientType) && clientNameMap.TryGetValue((clientType, clientId), out var resolvedName) && !string.IsNullOrWhiteSpace(resolvedName))
            return resolvedName;
        return clientId;
    }

    /// <summary>每条 SQL 的 IN 列表上限：SQL Server 单命令参数上限 2100，留足余量</summary>
    private const int NameResolveBatchSize = 500;

    private static readonly (string Type, string Table, string IdColumn, string NameExpression, string? OrgColumn)[] NameSources =
    {
        ("KH", "CRM客户", "F编号", "COALESCE(NULLIF(F简称, ''), NULLIF(F全称, ''), F编号)", "F组织ID"),
        ("DL", "EXP业务代理", "F编号", "COALESCE(NULLIF(F名称, ''), F编号)", null),
        ("WD", "EXP快递网点", "F编号", "COALESCE(NULLIF(F网点简称, ''), NULLIF(F网点全称, ''), F编号)", "F所属组织ID"),
        ("CB", "EXP承包区", "F编号", "COALESCE(NULLIF(F承包人, ''), F编号)", "F所属组织ID"),
        ("YZ", "EXP末端驿站", "F编号", "COALESCE(NULLIF(F名称, ''), F编号)", null),
        ("YW", "EXP业务员", "F工号", "COALESCE(NULLIF(F姓名, ''), F工号)", null),
    };

    private async Task<Dictionary<(string Type, string Id), string>> ResolveClientNamesAsync(IEnumerable<(string? Type, string? Id)> clientPairs)
    {
        var pairs = clientPairs
            .Where(p => !string.IsNullOrWhiteSpace(p.Type) && !string.IsNullOrWhiteSpace(p.Id))
            .Select(p => (Type: p.Type!, Id: p.Id!))
            .Distinct()
            .ToList();
        var result = new Dictionary<(string Type, string Id), string>();
        if (pairs.Count == 0)
            return result;

        var orgId = _orgContextAccessor.CurrentOrgId;
        var conn = _dbContext.Database.GetDbConnection();
        var shouldClose = conn.State != ConnectionState.Open;
        if (shouldClose)
            await conn.OpenAsync();

        try
        {
            foreach (var (type, table, idColumn, nameExpression, orgColumn) in NameSources)
            {
                var ids = pairs.Where(p => p.Type == type).Select(p => p.Id).Distinct().ToList();
                // 分批，避免超出 SQL Server 2100 参数上限
                foreach (var chunk in ids.Chunk(NameResolveBatchSize))
                {
                    using var cmd = conn.CreateCommand();
                    var parameterNames = new List<string>(chunk.Length);
                    for (var i = 0; i < chunk.Length; i++)
                    {
                        var parameterName = $"@id{i}";
                        parameterNames.Add(parameterName);
                        cmd.Parameters.Add(new SqlParameter(parameterName, chunk[i]));
                    }

                    var sql = $"SELECT {idColumn} AS Id, {nameExpression} AS Name FROM {table} WHERE {idColumn} IN ({string.Join(",", parameterNames)})";
                    if (!string.IsNullOrWhiteSpace(orgColumn) && orgId.HasValue)
                    {
                        sql += $" AND ({orgColumn} = @orgId OR {orgColumn} = 0)";
                        cmd.Parameters.Add(new SqlParameter("@orgId", orgId.Value));
                    }
                    cmd.CommandText = sql;

                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var id = reader.IsDBNull(0) ? "" : reader.GetString(0);
                        var name = reader.IsDBNull(1) ? "" : reader.GetString(1);
                        if (!string.IsNullOrWhiteSpace(id))
                            result[(type, id)] = name;
                    }
                }
            }
        }
        finally
        {
            if (shouldClose && conn.State == ConnectionState.Open)
                await conn.CloseAsync();
        }

        return result;
    }
}
