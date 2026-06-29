using STOTOP.Module.Finance.Dtos;

namespace STOTOP.Module.Finance.Services.Interfaces;

public interface IExchangeRateService
{
    /// <summary>获取账套的币种列表（去重）</summary>
    Task<List<CurrencyDto>> GetCurrenciesAsync(long accountSetId);

    /// <summary>获取汇率列表</summary>
    Task<List<ExchangeRateDto>> GetRatesAsync(long accountSetId, string? currencyCode = null);

    /// <summary>获取指定日期最近的汇率</summary>
    Task<ExchangeRateDto?> GetLatestRateAsync(long accountSetId, string currencyCode, DateTime date);

    /// <summary>保存/更新汇率</summary>
    Task<ExchangeRateDto> SaveRateAsync(SaveExchangeRateRequest request);

    /// <summary>删除汇率</summary>
    Task DeleteRateAsync(long id);
}
