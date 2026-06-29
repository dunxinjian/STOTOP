using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.System.Filters;
using STOTOP.Module.Finance.Services;

namespace STOTOP.Module.Finance.Controllers;

[Authorize]
[ApiController]
[Route("api/finance/journal")]
public class JournalController : ControllerBase
{
    private readonly JournalService _journalService;

    public JournalController(JournalService journalService)
    {
        _journalService = journalService;
    }

    /// <summary>全部日记账</summary>
    [HttpGet("")]
    public async Task<ApiResult<JournalPagedResult>> GetAll([FromQuery] JournalQueryRequest request)
    {
        var result = await _journalService.GetJournalEntriesAsync(request);
        return ApiResult<JournalPagedResult>.Success(result);
    }

    /// <summary>现金银行日记账（1001/1002）</summary>
    [HttpGet("cash-bank")]
    public async Task<ApiResult<JournalPagedResult>> GetCashBank([FromQuery] JournalQueryRequest request)
    {
        var result = await _journalService.GetCashBankJournalAsync(request);
        return ApiResult<JournalPagedResult>.Success(result);
    }

    /// <summary>应收应付日记账（1122/2202）</summary>
    [HttpGet("receivable-payable")]
    public async Task<ApiResult<JournalPagedResult>> GetReceivablePayable([FromQuery] JournalQueryRequest request)
    {
        var result = await _journalService.GetReceivablePayableJournalAsync(request);
        return ApiResult<JournalPagedResult>.Success(result);
    }

    /// <summary>新增日记账（收入/支出/收支）</summary>
    [HttpPost("create")]
    [RequirePermission(FinancePermissions.JournalCreate)]
    public async Task<ApiResult<long>> Create([FromBody] JournalCreateRequest request)
    {
        var voucherId = await _journalService.CreateAsync(request);
        return ApiResult<long>.Success(voucherId);
    }

    /// <summary>创建调整凭证</summary>
    [HttpPost("adjust")]
    [RequirePermission(FinancePermissions.JournalAdjust)]
    public async Task<ApiResult<long>> Adjust([FromBody] JournalAdjustRequest request)
    {
        var voucherId = await _journalService.AdjustAsync(request);
        return ApiResult<long>.Success(voucherId);
    }

    /// <summary>将草稿凭证提交（0 → 1）</summary>
    [HttpPost("generate-voucher")]
    public async Task<ApiResult> GenerateVoucher([FromBody] JournalGenerateVoucherRequest request)
    {
        var ok = await _journalService.GenerateVoucherAsync(request);
        return ok ? ApiResult.Ok("提交成功") : ApiResult.Fail("提交失败，未找到匹配凭证");
    }

    /// <summary>删除凭证（仅草稿/待审核）</summary>
    [HttpDelete("{voucherId}")]
    public async Task<ApiResult> Delete(long voucherId, [FromQuery] long accountSetId)
    {
        var ok = await _journalService.DeleteVoucherAsync(voucherId, accountSetId);
        return ok ? ApiResult.Ok("删除成功") : ApiResult.Fail("凭证不存在或已审核");
    }
}
