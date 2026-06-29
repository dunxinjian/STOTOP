using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Express.Dtos;
using STOTOP.Module.Express.Services;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.Express.Controllers;

/// <summary>
/// 预付款管理
/// </summary>
[Authorize]
[ApiController]
[Route("api/express/prepayment")]
public class PrepaymentController : ControllerBase
{
    private readonly IPrepaymentService _prepaymentService;

    public PrepaymentController(IPrepaymentService prepaymentService)
    {
        _prepaymentService = prepaymentService;
    }

    /// <summary>
    /// 查余额
    /// </summary>
    [HttpGet("balance/{clientId}")]
    [RequirePermission(ExpressPermissions.PrepaymentView)]
    public async Task<ApiResult<PrepaymentBalanceDto>> GetBalance(string clientId)
    {
        var result = await _prepaymentService.GetBalanceAsync(clientId);
        if (result == null)
            return ApiResult<PrepaymentBalanceDto>.Success(new PrepaymentBalanceDto { ClientId = clientId });
        return ApiResult<PrepaymentBalanceDto>.Success(result);
    }

    /// <summary>
    /// 查流水
    /// </summary>
    [HttpGet("transaction")]
    [RequirePermission(ExpressPermissions.PrepaymentView)]
    public async Task<ApiResult<PagedResult<PrepaymentTransactionDto>>> GetTransactions([FromQuery] TransactionQueryRequest request)
    {
        var result = await _prepaymentService.GetTransactionsAsync(request);
        return ApiResult<PagedResult<PrepaymentTransactionDto>>.Success(result);
    }

    /// <summary>
    /// 充值
    /// </summary>
    [HttpPost("recharge")]
    [RequirePermission(ExpressPermissions.PrepaymentCreate)]
    public async Task<ApiResult<PrepaymentDto>> Recharge([FromBody] RechargeRequest request)
    {
        try
        {
            var result = await _prepaymentService.RechargeAsync(
                request.ClientId, request.Amount, request.PaymentDate, request.PaymentMethod, request.Remark);
            return ApiResult<PrepaymentDto>.Success(result, "充值成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<PrepaymentDto>.Fail(ex.Message);
        }
    }
}
