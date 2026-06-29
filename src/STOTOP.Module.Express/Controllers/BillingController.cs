using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Services;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.Express.Controllers;

/// <summary>
/// 计费管理
/// </summary>
[Authorize]
[ApiController]
[Route("api/express/billing")]
public class BillingController : ControllerBase
{
    private readonly IBillingService _billingService;

    public BillingController(IBillingService billingService)
    {
        _billingService = billingService;
    }

    /// <summary>
    /// 执行计费（指定日期）
    /// </summary>
    [HttpPost("execute")]
    [RequirePermission(ExpressPermissions.BillingExecute)]
    public async Task<ApiResult<BillingExecutionResult>> Execute([FromBody] BillingExecutionRequest request)
    {
        try
        {
            var result = await _billingService.ExecuteBillingAsync(
                request.Waybills, request.SourceTable, request.BatchId, request.ResultTable);
            return ApiResult<BillingExecutionResult>.Success(result, "计费执行完成");
        }
        catch (Exception ex)
        {
            return ApiResult<BillingExecutionResult>.Fail($"计费执行失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 分页查询计费结果
    /// </summary>
    [HttpGet("results")]
    [RequirePermission(ExpressPermissions.BillingView)]
    public async Task<ApiResult<PagedResult<BillingResultListItemDto>>> GetResults([FromQuery] BillingResultQueryRequest request)
    {
        var result = await _billingService.GetResultListAsync(request);
        return ApiResult<PagedResult<BillingResultListItemDto>>.Success(result);
    }

    /// <summary>
    /// 获取计费结果详情
    /// </summary>
    [HttpGet("results/{id}")]
    [RequirePermission(ExpressPermissions.BillingView)]
    public async Task<ApiResult<BillingResultDto>> GetResultById(long id)
    {
        var result = await _billingService.GetResultByIdAsync(id);
        if (result == null)
            return ApiResult<BillingResultDto>.Fail("计费结果不存在");
        return ApiResult<BillingResultDto>.Success(result);
    }

    /// <summary>
    /// 按运单查询所有计费结果
    /// </summary>
    [HttpGet("results/by-waybill/{waybillId}")]
    [RequirePermission(ExpressPermissions.BillingView)]
    public async Task<ApiResult<List<BillingResultDto>>> GetResultsByWaybill(long waybillId)
    {
        var results = await _billingService.GetResultsByWaybillAsync(waybillId);
        return ApiResult<List<BillingResultDto>>.Success(results);
    }

    /// <summary>
    /// 查询异常运单统计
    /// </summary>
    [HttpGet("errors")]
    [RequirePermission(ExpressPermissions.BillingView)]
    public async Task<ApiResult<BillingErrorStatsDto>> GetErrorStats([FromQuery] string? brandCode)
    {
        var result = await _billingService.GetErrorStatsAsync(brandCode);
        return ApiResult<BillingErrorStatsDto>.Success(result);
    }

    /// <summary>
    /// 查询某类异常的运单明细
    /// </summary>
    [HttpGet("errors/detail")]
    [RequirePermission(ExpressPermissions.BillingView)]
    public async Task<ApiResult<PagedResult<BillingErrorDetailItemDto>>> GetErrorDetail([FromQuery] BillingErrorDetailRequest request)
    {
        if (string.IsNullOrEmpty(request.ErrorCode))
            return ApiResult<PagedResult<BillingErrorDetailItemDto>>.Fail("异常编码不能为空");
        var result = await _billingService.GetErrorDetailAsync(request);
        return ApiResult<PagedResult<BillingErrorDetailItemDto>>.Success(result);
    }

    /// <summary>
    /// 触发异常运单重算
    /// </summary>
    [HttpPost("retry")]
    [RequirePermission(ExpressPermissions.BillingExecute)]
    public async Task<ApiResult<BillingRetryResultDto>> Retry([FromBody] BillingRetryRequest request)
    {
        try
        {
            var result = await _billingService.RetryBillingAsync(request);
            return ApiResult<BillingRetryResultDto>.Success(result, result.Message);
        }
        catch (Exception ex)
        {
            return ApiResult<BillingRetryResultDto>.Fail($"重算失败: {ex.Message}");
        }
    }
}
