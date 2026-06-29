using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.CRM.Dtos;
using STOTOP.Module.CRM.Services.Interfaces;
using STOTOP.Module.System.Filters;

namespace STOTOP.Module.CRM.Controllers;

[Authorize]
[ApiController]
[Route("api/crm/service-orders")]
public class ServiceOrderController : ControllerBase
{
    private readonly IServiceOrderService _orderService;

    public ServiceOrderController(IServiceOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpGet]
    [RequirePermission(CrmPermissions.OrderView)]
    public async Task<ApiResult<PagedResult<ServiceOrderListItemDto>>> GetList([FromQuery] ServiceOrderQueryRequest request)
    {
        var result = await _orderService.GetServiceOrdersAsync(request);
        return ApiResult<PagedResult<ServiceOrderListItemDto>>.Success(result);
    }

    [HttpGet("{id}")]
    [RequirePermission(CrmPermissions.OrderView)]
    public async Task<ApiResult<ServiceOrderDto>> GetById(long id)
    {
        var result = await _orderService.GetServiceOrderByIdAsync(id);
        if (result == null)
            return ApiResult<ServiceOrderDto>.Fail("工单不存在");
        return ApiResult<ServiceOrderDto>.Success(result);
    }

    [HttpPost]
    [RequirePermission(CrmPermissions.OrderCreate)]
    public async Task<ApiResult<ServiceOrderDto>> Create([FromBody] CreateServiceOrderRequest request)
    {
        try
        {
            var result = await _orderService.CreateServiceOrderAsync(request);
            return ApiResult<ServiceOrderDto>.Success(result, "创建工单成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<ServiceOrderDto>.Fail(ex.Message);
        }
    }

    [HttpPut("{id}")]
    [RequirePermission(CrmPermissions.OrderEdit)]
    public async Task<ApiResult<ServiceOrderDto>> Update(long id, [FromBody] UpdateServiceOrderRequest request)
    {
        try
        {
            var result = await _orderService.UpdateServiceOrderAsync(id, request);
            if (result == null)
                return ApiResult<ServiceOrderDto>.Fail("工单不存在");
            return ApiResult<ServiceOrderDto>.Success(result, "更新工单成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<ServiceOrderDto>.Fail(ex.Message);
        }
    }

    [HttpDelete("{id}")]
    [RequirePermission(CrmPermissions.OrderEdit)]
    public async Task<ApiResult> Delete(long id)
    {
        var result = await _orderService.DeleteServiceOrderAsync(id);
        if (!result)
            return ApiResult.Fail("工单不存在");
        return ApiResult.Ok("删除工单成功");
    }

    [HttpPost("{id}/action")]
    [RequirePermission(CrmPermissions.OrderEdit)]
    public async Task<ApiResult> ExecuteAction(long id, [FromBody] ServiceOrderActionRequest request)
    {
        try
        {
            var result = await _orderService.ExecuteActionAsync(id, request);
            if (!result)
                return ApiResult.Fail("工单不存在");
            return ApiResult.Ok("操作成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    [HttpGet("statistics")]
    [RequirePermission(CrmPermissions.OrderView)]
    public async Task<ApiResult<ServiceOrderStatisticsDto>> GetStatistics([FromQuery] long? assigneeId)
    {
        var result = await _orderService.GetStatisticsAsync(assigneeId);
        return ApiResult<ServiceOrderStatisticsDto>.Success(result);
    }
}
