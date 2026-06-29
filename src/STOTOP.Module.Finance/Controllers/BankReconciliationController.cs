using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.System.Filters;
using STOTOP.Module.Finance.Services.Interfaces;

namespace STOTOP.Module.Finance.Controllers;

[Authorize]
[ApiController]
[Route("api/finance/bank")]
public class BankReconciliationController : ControllerBase
{
    private readonly IBankReconciliationService _bankReconciliationService;

    public BankReconciliationController(IBankReconciliationService bankReconciliationService)
    {
        _bankReconciliationService = bankReconciliationService;
    }

    [HttpPost("import")]
    [RequirePermission(FinancePermissions.BankReconciliationImport)]
    public async Task<ApiResult<object>> Import(
        IFormFile file,
        [FromForm] string? bankAccount,
        [FromForm] string? bankName,
        [FromForm] int startRow = 2,
        [FromForm] int dateColumnIndex = 0,
        [FromForm] int descriptionColumnIndex = 1,
        [FromForm] int debitColumnIndex = 2,
        [FromForm] int creditColumnIndex = 3,
        [FromForm] int balanceColumnIndex = 4,
        [FromForm] int counterpartyColumnIndex = 5,
        [FromForm] int referenceNoColumnIndex = -1,
        [FromQuery] long accountSetId = 0)
    {
        if (file == null || file.Length == 0)
            return ApiResult<object>.Fail("请选择要导入的文件");

        try
        {
            var request = new BankStatementImportRequest
            {
                BankAccount = bankAccount,
                BankName = bankName,
                StartRow = startRow,
                DateColumnIndex = dateColumnIndex,
                DescriptionColumnIndex = descriptionColumnIndex,
                DebitColumnIndex = debitColumnIndex,
                CreditColumnIndex = creditColumnIndex,
                BalanceColumnIndex = balanceColumnIndex,
                CounterpartyColumnIndex = counterpartyColumnIndex,
                ReferenceNoColumnIndex = referenceNoColumnIndex,
            };

            using var stream = file.OpenReadStream();
            var count = await _bankReconciliationService.ImportBankStatementsAsync(stream, request, accountSetId);
            return ApiResult<object>.Success(new { importCount = count }, $"成功导入 {count} 条银行流水");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<object>.Fail(ex.Message);
        }
    }

    [HttpGet("statements")]
    public async Task<ApiResult<BankStatementPagedResult>> GetStatements(
        [FromQuery] BankStatementQueryRequest request,
        [FromQuery] long accountSetId = 0)
    {
        var result = await _bankReconciliationService.GetStatementsAsync(request, accountSetId);
        return ApiResult<BankStatementPagedResult>.Success(result);
    }

    [HttpGet("unmatched-vouchers")]
    public async Task<ApiResult<List<UnmatchedVoucherDto>>> GetUnmatchedVouchers(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        [FromQuery] long accountSetId = 0)
    {
        var result = await _bankReconciliationService.GetUnmatchedVouchersAsync(startDate, endDate, accountSetId);
        return ApiResult<List<UnmatchedVoucherDto>>.Success(result);
    }

    [HttpPost("auto-match")]
    [RequirePermission(FinancePermissions.BankReconciliationMatch)]
    public async Task<ApiResult<object>> AutoMatch([FromQuery] long accountSetId = 0)
    {
        try
        {
            var count = await _bankReconciliationService.AutoMatchAsync(accountSetId);
            return ApiResult<object>.Success(new { matchCount = count }, $"自动匹配成功 {count} 笔");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<object>.Fail(ex.Message);
        }
    }

    [HttpPost("manual-match")]
    [RequirePermission(FinancePermissions.BankReconciliationMatch)]
    public async Task<ApiResult> ManualMatch([FromBody] ManualMatchRequest request, [FromQuery] long accountSetId = 0)
    {
        try
        {
            await _bankReconciliationService.ManualMatchAsync(request, accountSetId);
            return ApiResult.Ok("匹配成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    [HttpPost("unmatch/{id}")]
    [RequirePermission(FinancePermissions.BankReconciliationMatch)]
    public async Task<ApiResult> Unmatch(long id)
    {
        try
        {
            await _bankReconciliationService.UnmatchAsync(id);
            return ApiResult.Ok("取消匹配成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    [HttpGet("reconciliation-report")]
    public async Task<ApiResult<ReconciliationReportDto>> GetReconciliationReport(
        [FromQuery] long periodId,
        [FromQuery] long accountSetId = 0)
    {
        try
        {
            var result = await _bankReconciliationService.GetReconciliationReportAsync(periodId, accountSetId);
            return ApiResult<ReconciliationReportDto>.Success(result);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<ReconciliationReportDto>.Fail(ex.Message);
        }
    }
}
