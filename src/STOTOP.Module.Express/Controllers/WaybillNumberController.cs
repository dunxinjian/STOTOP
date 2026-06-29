using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Services;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.Express.Controllers;

/// <summary>
/// 运单号管理
/// </summary>
[Authorize]
[ApiController]
[Route("api/express/waybill-number")]
public class WaybillNumberController : ControllerBase
{
    private readonly IWaybillNumberService _waybillNumberService;

    public WaybillNumberController(IWaybillNumberService waybillNumberService)
    {
        _waybillNumberService = waybillNumberService;
    }

    /// <summary>
    /// 号段列表
    /// </summary>
    [HttpGet("pool")]
    [RequirePermission(ExpressPermissions.WaybillNumberView)]
    public async Task<ApiResult<List<WaybillNumberPoolDto>>> GetPoolList()
    {
        var result = await _waybillNumberService.GetPoolListAsync();
        return ApiResult<List<WaybillNumberPoolDto>>.Success(result);
    }

    /// <summary>
    /// 创建号段
    /// </summary>
    [HttpPost("pool")]
    [RequirePermission(ExpressPermissions.WaybillNumberCreate)]
    public async Task<ApiResult<WaybillNumberPoolDto>> CreatePool([FromBody] CreatePoolRequest request)
    {
        var result = await _waybillNumberService.CreatePoolAsync(request);
        return ApiResult<WaybillNumberPoolDto>.Success(result, "号段创建成功");
    }

    /// <summary>
    /// 分配运单号
    /// </summary>
    [HttpPost("allocate")]
    [RequirePermission(ExpressPermissions.WaybillNumberEdit)]
    public async Task<ApiResult<WaybillNumberTransactionDto>> Allocate([FromBody] AllocateRequest request)
    {
        try
        {
            var result = await _waybillNumberService.AllocateAsync(
                request.PoolId, request.ClientId, request.BrandCode, request.Quantity);
            return ApiResult<WaybillNumberTransactionDto>.Success(result, "分配成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<WaybillNumberTransactionDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 回收运单号
    /// </summary>
    [HttpPost("return")]
    [RequirePermission(ExpressPermissions.WaybillNumberEdit)]
    public async Task<ApiResult<WaybillNumberTransactionDto>> Return([FromBody] ReturnRequest request)
    {
        try
        {
            var result = await _waybillNumberService.ReturnAsync(
                request.ClientId, request.BrandCode, request.Quantity);
            return ApiResult<WaybillNumberTransactionDto>.Success(result, "回收成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<WaybillNumberTransactionDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 客户运单号余额
    /// </summary>
    [HttpGet("balance/{clientId}")]
    [RequirePermission(ExpressPermissions.WaybillNumberView)]
    public async Task<ApiResult<ClientWaybillBalanceDto>> GetBalance(string clientId, [FromQuery] string brandCode)
    {
        var result = await _waybillNumberService.GetClientBalanceAsync(clientId, brandCode);
        if (result == null)
            return ApiResult<ClientWaybillBalanceDto>.Success(new ClientWaybillBalanceDto { ClientId = clientId, BrandCode = brandCode });
        return ApiResult<ClientWaybillBalanceDto>.Success(result);
    }
}
