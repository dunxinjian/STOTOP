using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Dormitory.Dtos;
using STOTOP.Module.Dormitory.Services.Interfaces;

namespace STOTOP.Module.Dormitory.Controllers;

[Authorize]
[ApiController]
[Route("api/dormitory/expenses")]
public class ExpenseController : ControllerBase
{
    private readonly IExpenseService _expenseService;

    public ExpenseController(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    [HttpGet]
    public async Task<ApiResult<PagedResult<ExpenseListItemDto>>> GetList([FromQuery] ExpenseQueryRequest request)
    {
        var result = await _expenseService.GetExpensesAsync(request);
        return ApiResult<PagedResult<ExpenseListItemDto>>.Success(result);
    }

    [HttpGet("{id}")]
    public async Task<ApiResult<ExpenseDto>> GetById(long id)
    {
        var result = await _expenseService.GetExpenseByIdAsync(id);
        if (result == null)
        {
            return ApiResult<ExpenseDto>.Fail("费用记录不存在");
        }
        return ApiResult<ExpenseDto>.Success(result);
    }

    [HttpPost]
    public async Task<ApiResult<ExpenseDto>> Create([FromBody] CreateExpenseRequest request)
    {
        try
        {
            var result = await _expenseService.CreateExpenseAsync(request);
            return ApiResult<ExpenseDto>.Success(result, "创建费用记录成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<ExpenseDto>.Fail(ex.Message);
        }
    }

    [HttpPut("{id}")]
    public async Task<ApiResult<ExpenseDto>> Update(long id, [FromBody] UpdateExpenseRequest request)
    {
        var result = await _expenseService.UpdateExpenseAsync(id, request);
        if (result == null)
        {
            return ApiResult<ExpenseDto>.Fail("费用记录不存在");
        }
        return ApiResult<ExpenseDto>.Success(result, "更新费用记录成功");
    }

    [HttpDelete("{id}")]
    public async Task<ApiResult> Delete(long id)
    {
        var result = await _expenseService.DeleteExpenseAsync(id);
        if (!result)
        {
            return ApiResult.Fail("费用记录不存在");
        }
        return ApiResult.Ok("删除费用记录成功");
    }

    [HttpGet("monthly-summary")]
    public async Task<ApiResult<List<MonthlyExpenseSummaryDto>>> GetMonthlySummary([FromQuery] string? month)
    {
        var result = await _expenseService.GetMonthlySummaryAsync(month);
        return ApiResult<List<MonthlyExpenseSummaryDto>>.Success(result);
    }

    /// <summary>费用分摊明细：按房间当前在住人分摊（均摊/固定）</summary>
    [HttpGet("{id}/allocation")]
    public async Task<ApiResult<ExpenseAllocationDto>> GetAllocation(long id)
    {
        var result = await _expenseService.GetExpenseAllocationAsync(id);
        if (result == null)
        {
            return ApiResult<ExpenseAllocationDto>.Fail("费用记录不存在");
        }
        return ApiResult<ExpenseAllocationDto>.Success(result);
    }
}
