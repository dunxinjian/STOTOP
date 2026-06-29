using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Conference.Dtos;
using STOTOP.Module.Conference.Services.Interfaces;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.Conference.Controllers;

[Authorize]
[ApiController]
[Route("api/conference")]
public class FinanceController : ControllerBase
{
    private readonly IFinanceService _financeService;

    public FinanceController(IFinanceService financeService)
    {
        _financeService = financeService;
    }

    [HttpGet("events/{eventId}/incomes")]
    [RequirePermission(ConferencePermissions.IncomeView)]
    public async Task<ApiResult<PagedResult<IncomeListItemDto>>> GetIncomes(int eventId, [FromQuery] IncomeQueryRequest request)
    {
        var result = await _financeService.GetIncomesAsync(eventId, request);
        return ApiResult<PagedResult<IncomeListItemDto>>.Success(result);
    }

    [HttpGet("incomes/{id}")]
    [RequirePermission(ConferencePermissions.IncomeView)]
    public async Task<ApiResult<IncomeDto>> GetById(int id)
    {
        var result = await _financeService.GetIncomeByIdAsync(id);
        if (result == null)
            return ApiResult<IncomeDto>.Fail("收入记录不存在");
        return ApiResult<IncomeDto>.Success(result);
    }

    [HttpPost("events/{eventId}/incomes")]
    [RequirePermission(ConferencePermissions.IncomeCreate)]
    public async Task<ApiResult<IncomeDto>> Create(int eventId, [FromBody] CreateIncomeRequest request)
    {
        try
        {
            var result = await _financeService.CreateIncomeAsync(eventId, request);
            return ApiResult<IncomeDto>.Success(result, "创建收入记录成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<IncomeDto>.Fail(ex.Message);
        }
    }

    [HttpPut("incomes/{id}")]
    [RequirePermission(ConferencePermissions.IncomeEdit)]
    public async Task<ApiResult<IncomeDto>> Update(int id, [FromBody] UpdateIncomeRequest request)
    {
        try
        {
            var result = await _financeService.UpdateIncomeAsync(id, request);
            if (result == null)
                return ApiResult<IncomeDto>.Fail("收入记录不存在");
            return ApiResult<IncomeDto>.Success(result, "更新收入记录成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<IncomeDto>.Fail(ex.Message);
        }
    }

    [HttpDelete("incomes/{id}")]
    [RequirePermission(ConferencePermissions.IncomeDelete)]
    public async Task<ApiResult> Delete(int id)
    {
        var result = await _financeService.DeleteIncomeAsync(id);
        if (!result)
            return ApiResult.Fail("收入记录不存在");
        return ApiResult.Ok("删除收入记录成功");
    }

    [HttpGet("events/{eventId}/incomes/summary")]
    [RequirePermission(ConferencePermissions.IncomeView)]
    public async Task<ApiResult<IncomeSummaryDto>> GetSummary(int eventId)
    {
        var result = await _financeService.GetSummaryAsync(eventId);
        return ApiResult<IncomeSummaryDto>.Success(result);
    }

    [HttpGet("events/{eventId}/incomes/export")]
    [RequirePermission(ConferencePermissions.IncomeExport)]
    public async Task<IActionResult> Export(int eventId)
    {
        var bytes = await _financeService.ExportIncomesAsync(eventId);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "收入明细.xlsx");
    }

    [HttpPost("events/{eventId}/incomes/batch-register")]
    [RequirePermission(ConferencePermissions.IncomeCreate)]
    public async Task<ApiResult<int>> BatchRegister(int eventId, [FromBody] BatchRegisterIncomeRequest request)
    {
        var count = await _financeService.BatchRegisterAsync(eventId, request);
        return ApiResult<int>.Success(count, $"批量登记成功，共{count}条");
    }
}
