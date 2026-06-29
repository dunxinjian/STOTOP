using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.Dormitory.Dtos;
using STOTOP.Module.Dormitory.Services.Interfaces;

namespace STOTOP.Module.Dormitory.Controllers;

[Authorize]
[ApiController]
[Route("api/dormitory/repair-orders")]
public class RepairOrderController : ControllerBase
{
    private readonly IRepairOrderService _repairOrderService;

    public RepairOrderController(IRepairOrderService repairOrderService)
    {
        _repairOrderService = repairOrderService;
    }

    [HttpGet]
    public async Task<ApiResult<PagedResult<RepairOrderListItemDto>>> GetList([FromQuery] RepairOrderQueryRequest request)
    {
        var result = await _repairOrderService.GetRepairOrdersAsync(request);
        return ApiResult<PagedResult<RepairOrderListItemDto>>.Success(result);
    }

    [HttpGet("{id}")]
    public async Task<ApiResult<RepairOrderDto>> GetById(long id)
    {
        var result = await _repairOrderService.GetRepairOrderByIdAsync(id);
        if (result == null)
        {
            return ApiResult<RepairOrderDto>.Fail("报修工单不存在");
        }
        return ApiResult<RepairOrderDto>.Success(result);
    }

    [HttpPost]
    public async Task<ApiResult<RepairOrderDto>> Create([FromBody] CreateRepairOrderRequest request)
    {
        try
        {
            var result = await _repairOrderService.CreateRepairOrderAsync(request);
            return ApiResult<RepairOrderDto>.Success(result, "创建报修工单成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<RepairOrderDto>.Fail(ex.Message);
        }
    }

    [HttpPut("{id}")]
    public async Task<ApiResult<RepairOrderDto>> Update(long id, [FromBody] UpdateRepairOrderRequest request)
    {
        var result = await _repairOrderService.UpdateRepairOrderAsync(id, request);
        if (result == null)
        {
            return ApiResult<RepairOrderDto>.Fail("报修工单不存在");
        }
        return ApiResult<RepairOrderDto>.Success(result, "更新报修工单成功");
    }

    [HttpPut("{id}/handle")]
    public async Task<ApiResult<RepairOrderDto>> Handle(long id, [FromBody] HandleRepairOrderRequest request)
    {
        var result = await _repairOrderService.HandleRepairOrderAsync(id, request);
        if (result == null)
        {
            return ApiResult<RepairOrderDto>.Fail("报修工单不存在");
        }
        return ApiResult<RepairOrderDto>.Success(result, "处理报修工单成功");
    }

    [HttpDelete("{id}")]
    public async Task<ApiResult> Delete(long id)
    {
        var result = await _repairOrderService.DeleteRepairOrderAsync(id);
        if (!result)
        {
            return ApiResult.Fail("报修工单不存在");
        }
        return ApiResult.Ok("删除报修工单成功");
    }
}
