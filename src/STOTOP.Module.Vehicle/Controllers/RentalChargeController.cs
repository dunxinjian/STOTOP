using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.System.Filters;
using STOTOP.Module.Vehicle.Dtos;
using STOTOP.Module.Vehicle.Services.Interfaces;

namespace STOTOP.Module.Vehicle.Controllers;

/// <summary>
/// 租赁收费管理控制器
/// </summary>
[Authorize]
[ApiController]
[Route("api/vehicle/rental-charges")]
public class RentalChargeController : ControllerBase
{
    private readonly IRentalChargeService _rentalChargeService;

    public RentalChargeController(IRentalChargeService rentalChargeService)
    {
        _rentalChargeService = rentalChargeService;
    }

    /// <summary>
    /// 获取租赁收费记录列表（分页）
    /// </summary>
    [HttpGet]
    [RequirePermission(VehiclePermissions.RentalChargeView)]
    public async Task<ApiResult<PagedResult<RentalChargeListItemDto>>> GetList([FromQuery] RentalChargeQueryRequest request)
    {
        var result = await _rentalChargeService.GetChargesAsync(request);
        return ApiResult<PagedResult<RentalChargeListItemDto>>.Success(result);
    }

    /// <summary>
    /// 获取租赁收费记录详情
    /// </summary>
    [HttpGet("{id}")]
    [RequirePermission(VehiclePermissions.RentalChargeView)]
    public async Task<ApiResult<RentalChargeDto>> GetById(long id)
    {
        var result = await _rentalChargeService.GetChargeByIdAsync(id);
        if (result == null)
        {
            return ApiResult<RentalChargeDto>.Fail("租赁收费记录不存在");
        }
        return ApiResult<RentalChargeDto>.Success(result);
    }

    /// <summary>
    /// 批量生成租赁账单
    /// </summary>
    [HttpPost("generate")]
    [RequirePermission(VehiclePermissions.RentalChargeCreate)]
    public async Task<ApiResult<int>> Generate([FromBody] GenerateChargesRequest request)
    {
        try
        {
            var count = await _rentalChargeService.GenerateChargesAsync(request);
            return ApiResult<int>.Success(count, $"成功生成 {count} 条账单");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<int>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 确认收费
    /// </summary>
    [HttpPut("{id}/confirm")]
    [RequirePermission(VehiclePermissions.RentalChargeConfirm)]
    public async Task<ApiResult<RentalChargeDto>> Confirm(long id, [FromBody] ConfirmChargeRequest request)
    {
        try
        {
            var result = await _rentalChargeService.ConfirmChargeAsync(id, request);
            if (result == null)
            {
                return ApiResult<RentalChargeDto>.Fail("租赁收费记录不存在");
            }
            return ApiResult<RentalChargeDto>.Success(result, "确认收费成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<RentalChargeDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 减免收费
    /// </summary>
    [HttpPut("{id}/waive")]
    [RequirePermission(VehiclePermissions.RentalChargeWaive)]
    public async Task<ApiResult<RentalChargeDto>> Waive(long id, [FromBody] WaiveChargeRequest request)
    {
        try
        {
            var result = await _rentalChargeService.WaiveChargeAsync(id, request);
            if (result == null)
            {
                return ApiResult<RentalChargeDto>.Fail("租赁收费记录不存在");
            }
            return ApiResult<RentalChargeDto>.Success(result, "减免收费成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<RentalChargeDto>.Fail(ex.Message);
        }
    }
}
