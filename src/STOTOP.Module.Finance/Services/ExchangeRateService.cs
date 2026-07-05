using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STOTOP.Infrastructure.Data;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Entities;
using STOTOP.Module.Finance.Services.Interfaces;

namespace STOTOP.Module.Finance.Services;

public class ExchangeRateService : IExchangeRateService
{
    private readonly STOTOPDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<ExchangeRateService> _logger;

    public ExchangeRateService(STOTOPDbContext db, IHttpContextAccessor httpContextAccessor, ILogger<ExchangeRateService> logger)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    private long GetCurrentOrgId()
    {
        var orgIdObj = _httpContextAccessor.HttpContext?.Items["CurrentOrgId"];
        if (orgIdObj is long orgId) return orgId;
        return 0;
    }

    public async Task<List<CurrencyDto>> GetCurrenciesAsync(long accountSetId)
    {
        var currencies = await _db.Set<FinExchangeRate>()
            .Where(r => r.FAccountSetId == accountSetId)
            .Select(r => new { r.FCurrencyCode, r.FCurrencyName })
            .Distinct()
            .OrderBy(r => r.FCurrencyCode)
            .ToListAsync();

        return currencies.Select(c => new CurrencyDto
        {
            Code = c.FCurrencyCode,
            Name = c.FCurrencyName
        }).ToList();
    }

    public async Task<List<ExchangeRateDto>> GetRatesAsync(long accountSetId, string? currencyCode = null)
    {
        var query = _db.Set<FinExchangeRate>()
            .Where(r => r.FAccountSetId == accountSetId);

        if (!string.IsNullOrEmpty(currencyCode))
            query = query.Where(r => r.FCurrencyCode == currencyCode);

        var list = await query
            .OrderByDescending(r => r.FEffectiveDate)
            .ThenBy(r => r.FCurrencyCode)
            .ToListAsync();

        return list.Select(MapToDto).ToList();
    }

    public async Task<ExchangeRateDto?> GetLatestRateAsync(long accountSetId, string currencyCode, DateTime date)
    {
        var rate = await _db.Set<FinExchangeRate>()
            .Where(r => r.FAccountSetId == accountSetId
                     && r.FCurrencyCode == currencyCode
                     && r.FEffectiveDate <= date)
            .OrderByDescending(r => r.FEffectiveDate)
            .FirstOrDefaultAsync();

        return rate == null ? null : MapToDto(rate);
    }

    public async Task<ExchangeRateDto> SaveRateAsync(SaveExchangeRateRequest request)
    {
        FinExchangeRate entity;
        if (request.Id.HasValue && request.Id.Value > 0)
        {
            _logger.LogInformation("更新汇率 {RateId}, 币种: {Currency}, 汇率: {Rate}", request.Id.Value, request.CurrencyCode, request.Rate);
            // 经全局租户过滤器 + 显式账套归属校验取实体（FinExchangeRate 是 ITenantScoped，但 FindAsync 绕过滤器且不校验账套→IDOR）；
            // AsTracking 保留可更新语义（全局默认 NoTrackingWithIdentityResolution）
            entity = await _db.Set<FinExchangeRate>().AsTracking()
                .FirstOrDefaultAsync(r => r.FID == request.Id.Value && r.FAccountSetId == request.AccountSetId)
                ?? throw new InvalidOperationException("汇率记录不存在");
        }
        else
        {
            _logger.LogInformation("新增汇率, 币种: {Currency}, 汇率: {Rate}, 生效日期: {Date}", request.CurrencyCode, request.Rate, request.EffectiveDate);
            entity = new FinExchangeRate { FCreateTime = DateTime.Now };
            entity.FAccountSetId = request.AccountSetId;
            _db.Set<FinExchangeRate>().Add(entity);
        }

        entity.FCurrencyCode = request.CurrencyCode;
        entity.FCurrencyName = request.CurrencyName;
        entity.FRate = request.Rate;
        entity.FEffectiveDate = request.EffectiveDate;

        await _db.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task DeleteRateAsync(long id, long accountSetId)
    {
        _logger.LogInformation("删除汇率 {RateId}", id);
        // 经全局租户过滤器 + 显式账套归属校验取实体，禁止 FindAsync 绕过滤器越权删任意账套汇率
        var entity = await _db.Set<FinExchangeRate>().AsTracking()
            .FirstOrDefaultAsync(r => r.FID == id && r.FAccountSetId == accountSetId)
            ?? throw new InvalidOperationException("汇率记录不存在");
        _db.Set<FinExchangeRate>().Remove(entity);
        await _db.SaveChangesAsync();
        _logger.LogInformation("汇率 {RateId} 删除成功", id);
    }

    private static ExchangeRateDto MapToDto(FinExchangeRate e) => new()
    {
        Id = e.FID,
        AccountSetId = e.FAccountSetId,
        CurrencyCode = e.FCurrencyCode,
        CurrencyName = e.FCurrencyName,
        Rate = e.FRate,
        EffectiveDate = e.FEffectiveDate,
        CreateTime = e.FCreateTime
    };
}
