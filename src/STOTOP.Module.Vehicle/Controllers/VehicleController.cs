using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STOTOP.Core.Models;
using STOTOP.Module.System.Filters;
using STOTOP.Module.Vehicle.Dtos;
using STOTOP.Module.Vehicle.Services.Interfaces;

namespace STOTOP.Module.Vehicle.Controllers;

/// <summary>
/// 车辆台账管理控制器
/// </summary>
[Authorize]
[ApiController]
[Route("api/vehicle/vehicles")]
public class VehicleController : ControllerBase
{
    private readonly IVehicleService _vehicleService;

    public VehicleController(IVehicleService vehicleService)
    {
        _vehicleService = vehicleService;
    }

    /// <summary>
    /// 获取车辆列表（分页）
    /// </summary>
    [HttpGet]
    [RequirePermission(VehiclePermissions.VehicleView)]
    public async Task<ApiResult<PagedResult<VehicleListItemDto>>> GetList([FromQuery] VehicleQueryRequest request)
    {
        var result = await _vehicleService.GetVehiclesAsync(request);
        return ApiResult<PagedResult<VehicleListItemDto>>.Success(result);
    }

    /// <summary>
    /// 获取车辆详情
    /// </summary>
    [HttpGet("{id}")]
    [RequirePermission(VehiclePermissions.VehicleView)]
    public async Task<ApiResult<VehicleDto>> GetById(long id)
    {
        var result = await _vehicleService.GetVehicleByIdAsync(id);
        if (result == null)
        {
            return ApiResult<VehicleDto>.Fail("车辆不存在");
        }
        return ApiResult<VehicleDto>.Success(result);
    }

    /// <summary>
    /// 创建车辆
    /// </summary>
    [HttpPost]
    [RequirePermission(VehiclePermissions.VehicleCreate)]
    public async Task<ApiResult<VehicleDto>> Create([FromBody] CreateVehicleRequest request)
    {
        try
        {
            var result = await _vehicleService.CreateVehicleAsync(request);
            return ApiResult<VehicleDto>.Success(result, "创建车辆成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<VehicleDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 更新车辆
    /// </summary>
    [HttpPut("{id}")]
    [RequirePermission(VehiclePermissions.VehicleEdit)]
    public async Task<ApiResult<VehicleDto>> Update(long id, [FromBody] UpdateVehicleRequest request)
    {
        try
        {
            var result = await _vehicleService.UpdateVehicleAsync(id, request);
            if (result == null)
            {
                return ApiResult<VehicleDto>.Fail("车辆不存在");
            }
            return ApiResult<VehicleDto>.Success(result, "更新车辆成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult<VehicleDto>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 删除车辆
    /// </summary>
    [HttpDelete("{id}")]
    [RequirePermission(VehiclePermissions.VehicleDelete)]
    public async Task<ApiResult> Delete(long id)
    {
        try
        {
            var result = await _vehicleService.DeleteVehicleAsync(id);
            if (!result)
            {
                return ApiResult.Fail("车辆不存在");
            }
            return ApiResult.Ok("删除车辆成功");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 获取车辆统计信息
    /// </summary>
    [HttpGet("statistics")]
    [RequirePermission(VehiclePermissions.VehicleView)]
    public async Task<ApiResult<VehicleStatisticsDto>> GetStatistics()
    {
        var result = await _vehicleService.GetStatisticsAsync();
        return ApiResult<VehicleStatisticsDto>.Success(result);
    }
}
