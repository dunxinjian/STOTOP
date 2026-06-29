using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Services;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.Express.Controllers;

/// <summary>
/// 数据质量中心：待配置店铺 / 空店铺账号 / 一键完成 / 重新计费
/// </summary>
[Authorize]
[ApiController]
[Route("api/express/quality-center")]
public class QualityCenterController : ControllerBase
{
    private readonly IQualityCenterService _service;
    private readonly ILogger<QualityCenterController> _logger;

    public QualityCenterController(IQualityCenterService service, ILogger<QualityCenterController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>质量中心总览</summary>
    [HttpGet("overview")]
    [RequirePermission(ExpressPermissions.QualityCenterView)]
    public async Task<ApiResult<QualityCenterOverviewDto>> GetOverview()
    {
        try
        {
            var result = await _service.GetOverviewAsync();
            return ApiResult<QualityCenterOverviewDto>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取质量中心总览失败");
            return ApiResult<QualityCenterOverviewDto>.Fail($"获取失败: {ex.Message}", 500);
        }
    }

    /// <summary>待配置店铺列表</summary>
    [HttpGet("pending-shops")]
    [RequirePermission(ExpressPermissions.QualityCenterView)]
    public async Task<ApiResult<PagedResult<PendingShopItemDto>>> GetPendingShops([FromQuery] PendingShopQueryRequest request)
    {
        try
        {
            var result = await _service.GetPendingShopsAsync(request);
            return ApiResult<PagedResult<PendingShopItemDto>>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查询待配置店铺失败");
            return ApiResult<PagedResult<PendingShopItemDto>>.Fail($"查询失败: {ex.Message}", 500);
        }
    }

    /// <summary>一键完成店铺归属+报价校验</summary>
    [HttpPost("pending-shops/complete")]
    [RequirePermission(ExpressPermissions.QualityCenterManage)]
    public async Task<ApiResult<CompleteShopConfigResultDto>> CompleteShopConfig([FromBody] CompleteShopConfigRequest request)
    {
        try
        {
            var result = await _service.CompleteShopConfigAsync(request);
            var msg = result.PricePlanWarning == null ? "店铺配置已完成" : "店铺归属已配置，但报价方案检查有提示";
            return ApiResult<CompleteShopConfigResultDto>.Success(result, msg);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<CompleteShopConfigResultDto>.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "完成店铺配置失败: ShopName={ShopName}", request.ShopName);
            return ApiResult<CompleteShopConfigResultDto>.Fail($"配置失败: {ex.Message}", 500);
        }
    }

    /// <summary>空店铺账号运单列表</summary>
    [HttpGet("empty-shop-rows")]
    [RequirePermission(ExpressPermissions.QualityCenterView)]
    public async Task<ApiResult<PagedResult<EmptyShopRowItemDto>>> GetEmptyShopRows([FromQuery] EmptyShopRowQueryRequest request)
    {
        try
        {
            var result = await _service.GetEmptyShopRowsAsync(request);
            return ApiResult<PagedResult<EmptyShopRowItemDto>>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查询空店铺账号运单失败");
            return ApiResult<PagedResult<EmptyShopRowItemDto>>.Fail($"查询失败: {ex.Message}", 500);
        }
    }

    /// <summary>批量补填空店铺账号</summary>
    [HttpPost("empty-shop-rows/fill")]
    [RequirePermission(ExpressPermissions.QualityCenterManage)]
    public async Task<ApiResult<EmptyShopRowBatchResultDto>> FillEmptyShopAccount([FromBody] FillEmptyShopAccountRequest request)
    {
        try
        {
            var result = await _service.FillEmptyShopAccountAsync(request);
            return ApiResult<EmptyShopRowBatchResultDto>.Success(result, result.Message);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<EmptyShopRowBatchResultDto>.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "补填店铺账号失败");
            return ApiResult<EmptyShopRowBatchResultDto>.Fail($"补填失败: {ex.Message}", 500);
        }
    }

    /// <summary>批量忽略空店铺账号</summary>
    [HttpPost("empty-shop-rows/ignore")]
    [RequirePermission(ExpressPermissions.QualityCenterManage)]
    public async Task<ApiResult<EmptyShopRowBatchResultDto>> IgnoreEmptyShopRows([FromBody] IgnoreEmptyShopRowsRequest request)
    {
        try
        {
            var result = await _service.IgnoreEmptyShopRowsAsync(request);
            return ApiResult<EmptyShopRowBatchResultDto>.Success(result, result.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "忽略空店铺账号失败");
            return ApiResult<EmptyShopRowBatchResultDto>.Fail($"忽略失败: {ex.Message}", 500);
        }
    }

    /// <summary>重新计费（复用 ImportService.RetryBatch 重跑整个管道）</summary>
    [HttpPost("rerun-billing")]
    [RequirePermission(ExpressPermissions.QualityCenterRerun)]
    public async Task<ApiResult<RerunBillingResultDto>> RerunBilling([FromBody] RerunBillingRequest request)
    {
        try
        {
            var result = await _service.RerunBillingAsync(request);
            return ApiResult<RerunBillingResultDto>.Success(result, result.Message);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<RerunBillingResultDto>.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重新计费失败: BatchId={BatchId}", request.BatchId);
            return ApiResult<RerunBillingResultDto>.Fail($"重计失败: {ex.Message}", 500);
        }
    }

    // ========== 未识别网点 ==========

    /// <summary>未识别网点列表</summary>
    [HttpGet("unrecognized-network-points")]
    [RequirePermission(ExpressPermissions.QualityCenterView)]
    public async Task<ApiResult<PagedResult<UnrecognizedNetworkPointItemDto>>> GetUnrecognizedNetworkPoints([FromQuery] UnrecognizedNetworkPointQueryRequest request)
    {
        try
        {
            var result = await _service.GetUnrecognizedNetworkPointsAsync(request);
            return ApiResult<PagedResult<UnrecognizedNetworkPointItemDto>>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查询未识别网点失败");
            return ApiResult<PagedResult<UnrecognizedNetworkPointItemDto>>.Fail($"查询失败: {ex.Message}", 500);
        }
    }

    /// <summary>关联网点</summary>
    [HttpPost("associate-network-point")]
    [RequirePermission(ExpressPermissions.QualityCenterManage)]
    public async Task<ApiResult<AssociateNetworkPointResultDto>> AssociateNetworkPoint([FromBody] AssociateNetworkPointRequest request)
    {
        try
        {
            var result = await _service.AssociateNetworkPointAsync(request);
            return ApiResult<AssociateNetworkPointResultDto>.Success(result, result.Message);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<AssociateNetworkPointResultDto>.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "关联网点失败: Name={Name}", request.NetworkPointName);
            return ApiResult<AssociateNetworkPointResultDto>.Fail($"关联失败: {ex.Message}", 500);
        }
    }

    /// <summary>批量忽略网点错误</summary>
    [HttpPost("ignore-network-point-errors")]
    [RequirePermission(ExpressPermissions.QualityCenterManage)]
    public async Task<ApiResult<IgnoreNetworkPointErrorsResultDto>> IgnoreNetworkPointErrors([FromBody] IgnoreNetworkPointErrorsRequest request)
    {
        try
        {
            var result = await _service.IgnoreNetworkPointErrorsAsync(request);
            return ApiResult<IgnoreNetworkPointErrorsResultDto>.Success(result, result.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "忽略网点错误失败");
            return ApiResult<IgnoreNetworkPointErrorsResultDto>.Fail($"忽略失败: {ex.Message}", 500);
        }
    }

    // ========== 网点不一致 ==========

    /// <summary>分页查询网点不一致记录</summary>
    [HttpGet("network-point-mismatches")]
    [RequirePermission(ExpressPermissions.QualityCenterView)]
    public async Task<ApiResult<PagedResult<NetworkPointMismatchItemDto>>> GetNetworkPointMismatches([FromQuery] NetworkPointMismatchQueryDto query)
    {
        try
        {
            var result = await _service.GetNetworkPointMismatchesAsync(query);
            return ApiResult<PagedResult<NetworkPointMismatchItemDto>>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查询网点不一致记录失败");
            return ApiResult<PagedResult<NetworkPointMismatchItemDto>>.Fail($"查询失败: {ex.Message}", 500);
        }
    }

    /// <summary>批量忽略网点不一致记录</summary>
    [HttpPost("network-point-mismatches/ignore")]
    [RequirePermission(ExpressPermissions.QualityCenterManage)]
    public async Task<ApiResult<object>> IgnoreMismatchErrors([FromBody] IgnoreMismatchErrorsRequest request)
    {
        try
        {
            var count = await _service.IgnoreMismatchErrorsAsync(request.ErrorIds);
            return ApiResult<object>.Success(new { AffectedCount = count }, $"已将 {count} 条网点不一致记录标记为忽略");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "忽略网点不一致错误失败");
            return ApiResult<object>.Fail($"忽略失败: {ex.Message}", 500);
        }
    }
}
