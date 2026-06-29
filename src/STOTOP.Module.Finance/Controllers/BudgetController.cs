using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Finance.Dtos;
using STOTOP.Module.Finance.Services.Interfaces;

namespace STOTOP.Module.Finance.Controllers;

[Authorize]
[ApiController]
[Route("api/finance/budgets")]
public class BudgetController : ControllerBase
{
    private readonly IBudgetService _budgetService;
    private readonly IBudgetExpenseMappingService _mappingService;

    public BudgetController(
        IBudgetService budgetService,
        IBudgetExpenseMappingService mappingService)
    {
        _budgetService = budgetService;
        _mappingService = mappingService;
    }

    [HttpGet("versions")]
    public async Task<ApiResult<List<BudgetVersionDto>>> GetVersions(
        [FromQuery] long accountSetId,
        [FromQuery] int? year)
    {
        var result = await _budgetService.GetVersionsAsync(accountSetId, year);
        return ApiResult<List<BudgetVersionDto>>.Success(result);
    }

    [HttpPost("versions")]
    public async Task<ApiResult<BudgetVersionDto>> CreateVersion([FromBody] CreateBudgetVersionRequest request)
    {
        try
        {
            var result = await _budgetService.CreateVersionAsync(request, User.Identity?.Name);
            return ApiResult<BudgetVersionDto>.Success(result, "创建预算版本成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<BudgetVersionDto>.Fail(ex.Message);
        }
    }

    [HttpPost("versions/{id}/submit")]
    public async Task<ApiResult> SubmitVersion(long id)
    {
        try
        {
            await _budgetService.SubmitVersionAsync(id);
            return ApiResult.Ok("提交预算版本成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    [HttpPost("versions/{id}/approve")]
    public async Task<ApiResult> ApproveVersion(long id)
    {
        try
        {
            await _budgetService.ApproveVersionAsync(id, User.Identity?.Name);
            return ApiResult.Ok("审批预算版本成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    [HttpGet("versions/{id}/lines")]
    public async Task<ApiResult<List<BudgetLineDto>>> GetLines(
        long id,
        [FromQuery] string? period,
        [FromQuery] long? orgId)
    {
        try
        {
            var result = await _budgetService.GetLinesAsync(id, period, orgId);
            return ApiResult<List<BudgetLineDto>>.Success(result);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<List<BudgetLineDto>>.Fail(ex.Message);
        }
    }

    [HttpPost("versions/{id}/lines:batch-upsert")]
    public async Task<ApiResult> BatchUpsertLines(long id, [FromBody] BatchUpsertBudgetLinesRequest request)
    {
        try
        {
            await _budgetService.BatchUpsertLinesAsync(id, request);
            return ApiResult.Ok("保存预算明细成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    [HttpGet("expense-mappings")]
    public async Task<ApiResult<List<BudgetExpenseMappingDto>>> GetExpenseMappings(
        [FromQuery] long accountSetId,
        [FromQuery] long? orgId)
    {
        var result = await _mappingService.GetMappingsAsync(accountSetId, orgId);
        return ApiResult<List<BudgetExpenseMappingDto>>.Success(result);
    }

    [HttpPost("expense-mappings")]
    public async Task<ApiResult<BudgetExpenseMappingDto>> SaveExpenseMapping([FromBody] BudgetExpenseMappingDto request)
    {
        try
        {
            var result = await _mappingService.SaveMappingAsync(request);
            return ApiResult<BudgetExpenseMappingDto>.Success(result, "保存费用映射成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<BudgetExpenseMappingDto>.Fail(ex.Message);
        }
    }
}
