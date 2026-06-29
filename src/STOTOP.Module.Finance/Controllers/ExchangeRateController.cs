using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Models;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Services.Interfaces;

namespace STOTOP.Module.Finance.Controllers;

[Authorize]
[ApiController]
[Route("api/finance/exchange-rates")]
public class ExchangeRateController : ControllerBase
{
    private readonly IExchangeRateService _service;
    private readonly ILogger<ExchangeRateController> _logger;

    public ExchangeRateController(IExchangeRateService service, ILogger<ExchangeRateController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>获取账套币种列表（去重）</summary>
    [HttpGet("currencies")]
    public async Task<ApiResult<List<CurrencyDto>>> GetCurrencies([FromQuery] long accountSetId)
    {
        var result = await _service.GetCurrenciesAsync(accountSetId);
        return ApiResult<List<CurrencyDto>>.Success(result);
    }

    /// <summary>获取汇率列表</summary>
    [HttpGet]
    public async Task<ApiResult<List<ExchangeRateDto>>> GetRates([FromQuery] long accountSetId, [FromQuery] string? currencyCode = null)
    {
        var result = await _service.GetRatesAsync(accountSetId, currencyCode);
        return ApiResult<List<ExchangeRateDto>>.Success(result);
    }

    /// <summary>获取指定日期最近汇率</summary>
    [HttpGet("latest")]
    public async Task<ApiResult<ExchangeRateDto?>> GetLatestRate(
        [FromQuery] long accountSetId,
        [FromQuery] string currencyCode,
        [FromQuery] DateTime date)
    {
        var result = await _service.GetLatestRateAsync(accountSetId, currencyCode, date);
        return ApiResult<ExchangeRateDto?>.Success(result);
    }

    /// <summary>新增/更新汇率</summary>
    [HttpPost]
    public async Task<ApiResult<ExchangeRateDto>> SaveRate([FromBody] SaveExchangeRateRequest request)
    {
        try
        {
            var result = await _service.SaveRateAsync(request);
            return ApiResult<ExchangeRateDto>.Success(result, "保存成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<ExchangeRateDto>.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存汇率失败");
            return ApiResult<ExchangeRateDto>.Fail("操作失败，请稍后重试");
        }
    }

    /// <summary>删除汇率</summary>
    [HttpDelete("{id}")]
    public async Task<ApiResult<bool>> DeleteRate(long id)
    {
        try
        {
            await _service.DeleteRateAsync(id);
            return ApiResult<bool>.Success(true, "删除成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<bool>.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除汇率失败");
            return ApiResult<bool>.Fail("操作失败，请稍后重试");
        }
    }
}
