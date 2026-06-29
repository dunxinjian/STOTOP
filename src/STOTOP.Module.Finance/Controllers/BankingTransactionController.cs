using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Services.Interfaces;

namespace STOTOP.Module.Finance.Controllers;

[Authorize]
[ApiController]
[Route("api/finance/banking-transactions")]
public class BankingTransactionController : ControllerBase
{
    private readonly IBankTransactionService _bankTransactionService;

    public BankingTransactionController(IBankTransactionService bankTransactionService)
    {
        _bankTransactionService = bankTransactionService;
    }

    [HttpGet]
    public async Task<ApiResult<PagedResult<BankTransactionDto>>> GetList([FromQuery] BankTransactionQueryRequest request)
    {
        var result = await _bankTransactionService.GetTransactionsAsync(request);
        return ApiResult<PagedResult<BankTransactionDto>>.Success(result);
    }

    [HttpGet("{id}")]
    public async Task<ApiResult<BankTransactionDto>> GetById(long id)
    {
        var result = await _bankTransactionService.GetTransactionByIdAsync(id);
        if (result == null)
        {
            return ApiResult<BankTransactionDto>.Fail("银行流水不存在");
        }
        return ApiResult<BankTransactionDto>.Success(result);
    }

    [HttpPost("import")]
    public async Task<ApiResult<BankTransactionImportResult>> Import([FromBody] BankTransactionImportRequest request)
    {
        var operatorName = User.Identity?.Name;
        var result = await _bankTransactionService.ImportTransactionsAsync(request, operatorName);
        return ApiResult<BankTransactionImportResult>.Success(result, "导入完成");
    }

    [HttpPost("auto-match")]
    public async Task<ApiResult<AutoMatchResult>> AutoMatch()
    {
        var result = await _bankTransactionService.AutoMatchAsync();
        return ApiResult<AutoMatchResult>.Success(result, "自动匹配完成");
    }

    [HttpPost("manual-match")]
    public async Task<ApiResult> ManualMatch([FromBody] BankTransactionManualMatchRequest request)
    {
        try
        {
            var operatorName = User.Identity?.Name;
            var result = await _bankTransactionService.ManualMatchAsync(request, operatorName);
            if (!result)
            {
                return ApiResult.Fail("银行流水不存在");
            }
            return ApiResult.Ok("人工匹配成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    [HttpPost("skip-match")]
    public async Task<ApiResult<int>> SkipMatch([FromBody] BankTransactionSkipMatchRequest request)
    {
        var operatorName = User.Identity?.Name;
        var count = await _bankTransactionService.SkipMatchAsync(request, operatorName);
        return ApiResult<int>.Success(count, $"已标记 {count} 条流水为无需匹配");
    }
}
