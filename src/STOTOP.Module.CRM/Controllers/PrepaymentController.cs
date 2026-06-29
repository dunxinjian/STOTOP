using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.CRM.Dtos;
using STOTOP.Module.CRM.Services.Interfaces;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.CRM.Controllers;

[Authorize]
[ApiController]
[Route("api/crm/prepayments")]
public class PrepaymentController : ControllerBase
{
    private readonly IPrepaymentWaybillService _service;

    public PrepaymentController(IPrepaymentWaybillService service)
    {
        _service = service;
    }

    [HttpGet]
    [RequirePermission(CrmPermissions.PrepaymentView)]
    public async Task<ApiResult<PagedResult<PrepaymentDto>>> GetList([FromQuery] PrepaymentQueryRequest request)
    {
        var result = await _service.GetPrepaymentsAsync(request);
        return ApiResult<PagedResult<PrepaymentDto>>.Success(result);
    }

    [HttpGet("{id}")]
    [RequirePermission(CrmPermissions.PrepaymentView)]
    public async Task<ApiResult<PrepaymentDto>> GetById(long id)
    {
        var result = await _service.GetPrepaymentByIdAsync(id);
        if (result == null)
            return ApiResult<PrepaymentDto>.Fail("预付款记录不存在");
        return ApiResult<PrepaymentDto>.Success(result);
    }

    [HttpPost]
    [RequirePermission(CrmPermissions.PrepaymentCreate)]
    public async Task<ApiResult<PrepaymentDto>> Create([FromBody] CreatePrepaymentRequest request)
    {
        var result = await _service.CreatePrepaymentAsync(request);
        return ApiResult<PrepaymentDto>.Success(result, "创建预付款记录成功");
    }

    [HttpPut("{id}/confirm")]
    [RequirePermission(CrmPermissions.PrepaymentAllocate)]
    public async Task<ApiResult> Confirm(long id, [FromBody] ConfirmPrepaymentRequest request)
    {
        var result = await _service.ConfirmPrepaymentReceivedAsync(id, request.ReceivedAmount, request.BankTransactionId);
        if (!result)
            return ApiResult.Fail("预付款记录不存在");
        return ApiResult.Ok("确认到账成功");
    }

    [HttpGet("account")]
    [RequirePermission(CrmPermissions.PrepaymentView)]
    public async Task<ApiResult<CustomerAccountDto>> GetAccount([FromQuery] string customerId, [FromQuery] string brandCode)
    {
        var result = await _service.GetCustomerAccountAsync(customerId, brandCode);
        if (result == null)
            return ApiResult<CustomerAccountDto>.Fail("客户账户不存在");
        return ApiResult<CustomerAccountDto>.Success(result);
    }

    [HttpGet("allocations/customer/{customerId}")]
    [RequirePermission(CrmPermissions.PrepaymentView)]
    public async Task<ApiResult<List<WaybillAllocationDto>>> GetCustomerAllocations(string customerId)
    {
        var result = await _service.GetAllocationsByCustomerAsync(customerId);
        return ApiResult<List<WaybillAllocationDto>>.Success(result);
    }
}

public class ConfirmPrepaymentRequest
{
    public decimal ReceivedAmount { get; set; }
    public long? BankTransactionId { get; set; }
}
