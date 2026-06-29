using Microsoft.EntityFrameworkCore;
using STOTOP.Core.Interfaces;
using STOTOP.Core.Models;
using STOTOP.Module.CRM.Dtos;
using STOTOP.Module.CRM.Entities;
using STOTOP.Module.CRM.Services.Interfaces;

namespace STOTOP.Module.CRM.Services;

public class ProfitCalcService : IProfitCalcService
{
    private readonly IRepository<CrmCustomerProfit> _profitRepo;
    private readonly IRepository<CrmCustomer> _customerRepo;

    public ProfitCalcService(
        IRepository<CrmCustomerProfit> profitRepo,
        IRepository<CrmCustomer> customerRepo)
    {
        _profitRepo = profitRepo;
        _customerRepo = customerRepo;
    }

    public async Task<PagedResult<CustomerProfitDto>> GetProfitsAsync(ProfitQueryRequest request)
    {
        var query = _profitRepo.Query()
            .Include(p => p.Customer)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.CustomerId))
            query = query.Where(p => p.FCustomerId == request.CustomerId);
        if (request.OrgId.HasValue)
            query = query.Where(p => p.FOrgId == request.OrgId.Value);
        if (!string.IsNullOrWhiteSpace(request.Period))
            query = query.Where(p => p.FPeriod == request.Period);
        if (request.StartDate.HasValue)
            query = query.Where(p => p.FCreatedTime >= request.StartDate.Value);
        if (request.EndDate.HasValue)
            query = query.Where(p => p.FCreatedTime <= request.EndDate.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(p => p.FCreatedTime)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<CustomerProfitDto>
        {
            Items = items.Select(MapToDto).ToList(),
            Total = total,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        };
    }

    public async Task<CustomerProfitDto?> GetProfitByIdAsync(long id)
    {
        var entity = await _profitRepo.Query()
            .Include(p => p.Customer)
            .FirstOrDefaultAsync(p => p.FID == id);
        return entity == null ? null : MapToDto(entity);
    }

    public async Task<CustomerProfitDto> CreateProfitAsync(CreateProfitRequest request)
    {
        var profit = request.Revenue - request.Cost;
        var profitRate = request.Revenue != 0 ? Math.Round(profit / request.Revenue * 100, 2) : 0;

        var entity = new CrmCustomerProfit
        {
            FCustomerId = request.CustomerId,
            FOrgId = request.OrgId ?? 0,
            FPeriod = request.Period,
            FRevenue = request.Revenue,
            FCost = request.Cost,
            FProfit = profit,
            FProfitRate = profitRate,
            FDataSource = 2, // 手动录入
            FCreatedTime = DateTime.Now
        };

        await _profitRepo.AddAsync(entity);
        return (await GetProfitByIdAsync(entity.FID))!;
    }

    public async Task<CustomerProfitDto?> UpdateProfitAsync(long id, CreateProfitRequest request)
    {
        var entity = await _profitRepo.Query().AsTracking()
            .FirstOrDefaultAsync(p => p.FID == id);
        if (entity == null) return null;

        var profit = request.Revenue - request.Cost;
        var profitRate = request.Revenue != 0 ? Math.Round(profit / request.Revenue * 100, 2) : 0;

        entity.FCustomerId = request.CustomerId;
        entity.FOrgId = request.OrgId ?? 0;
        entity.FPeriod = request.Period;
        entity.FRevenue = request.Revenue;
        entity.FCost = request.Cost;
        entity.FProfit = profit;
        entity.FProfitRate = profitRate;
        entity.FUpdatedTime = DateTime.Now;

        await _profitRepo.UpdateAsync(entity);
        return await GetProfitByIdAsync(id);
    }

    public async Task<bool> DeleteProfitAsync(long id)
    {
        var entity = await _profitRepo.GetByIdAsync(id);
        if (entity == null) return false;
        await _profitRepo.DeleteAsync(id);
        return true;
    }

    public async Task<List<ProfitSummaryDto>> GetProfitSummaryAsync(long? orgId, string? period)
    {
        var query = _profitRepo.Query().AsQueryable();

        if (orgId.HasValue)
            query = query.Where(p => p.FOrgId == orgId.Value);
        if (!string.IsNullOrWhiteSpace(period))
            query = query.Where(p => p.FPeriod == period);

        var result = await query
            .GroupBy(p => new { p.FOrgId, p.FPeriod })
            .Select(g => new ProfitSummaryDto
            {
                OrgId = g.Key.FOrgId,
                Period = g.Key.FPeriod,
                TotalRevenue = g.Sum(p => p.FRevenue),
                TotalCost = g.Sum(p => p.FCost),
                TotalProfit = g.Sum(p => p.FProfit),
                AvgProfitRate = g.Average(p => p.FProfitRate),
                CustomerCount = g.Select(p => p.FCustomerId).Distinct().Count()
            })
            .OrderByDescending(s => s.TotalProfit)
            .ToListAsync();

        return result;
    }

    public async Task<List<ProfitRankingDto>> GetProfitRankingAsync(long? orgId, string? period, int top = 20)
    {
        var query = _profitRepo.Query()
            .Include(p => p.Customer)
            .AsQueryable();

        if (orgId.HasValue)
            query = query.Where(p => p.FOrgId == orgId.Value);
        if (!string.IsNullOrWhiteSpace(period))
            query = query.Where(p => p.FPeriod == period);

        var result = await query
            .GroupBy(p => new { p.FCustomerId, CustomerName = p.Customer.FShortName })
            .Select(g => new ProfitRankingDto
            {
                CustomerId = g.Key.FCustomerId,
                CustomerName = g.Key.CustomerName,
                TotalProfit = g.Sum(p => p.FProfit),
                TotalRevenue = g.Sum(p => p.FRevenue),
                AvgProfitRate = g.Average(p => p.FProfitRate)
            })
            .OrderByDescending(r => r.TotalProfit)
            .Take(top)
            .ToListAsync();

        return result;
    }

    /// <summary>
    /// 预留：后续对接 EXP 计费数据时实现具体计算逻辑
    /// </summary>
    public Task CalcProfitAsync(string customerId, string period)
    {
        // TODO: 对接 EXP 模块计费数据，自动计算毛利
        throw new NotImplementedException("毛利自动计算功能待对接 EXP 模块后实现");
    }

    #region Mapping

    private static CustomerProfitDto MapToDto(CrmCustomerProfit e) => new()
    {
        Id = e.FID,
        CustomerId = e.FCustomerId,
        CustomerName = e.Customer?.FShortName,
        OrgId = e.FOrgId,
        Period = e.FPeriod,
        Revenue = e.FRevenue,
        Cost = e.FCost,
        Profit = e.FProfit,
        ProfitRate = e.FProfitRate,
        DataSource = e.FDataSource,
        CreatorName = e.FCreatorName,
        CreatedTime = e.FCreatedTime
    };

    #endregion
}
